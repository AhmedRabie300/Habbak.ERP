using System.Security.Cryptography;
using System.Text;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.Employees.Commands.UpdateEmployee;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Infrastructure.Persistence;
using Habbak.ERP.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests.HR;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B2 — Employee + EmployeePersonalData. Uses a
/// real PiiSecretProtector (ephemeral local Data Protection keys, no Vault dependency — same
/// technique as 0.3's EncryptedStringConverterTests) and a deterministic stub IPiiHasher (avoids
/// coupling basic entity tests to a running Vault, unlike the dedicated Vault test suite).
/// </summary>
public sealed class EmployeeBatch2Tests : IAsyncLifetime
{
    private sealed class StubPiiHasher : IPiiHasher
    {
        public Task<string> ComputeHashAsync(string value, CancellationToken cancellationToken = default) =>
            Task.FromResult(Compute(value));

        public Task<IReadOnlyList<string>> ComputeHashCandidatesAsync(string value, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([Compute(value)]);

        private static string Compute(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"test-hmac-key:{value}"))).ToLowerInvariant();
    }

    private readonly string _databaseName = $"HabbakErpTests_HrB2_{Guid.NewGuid():N}";
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"habbak-dp-test-{Guid.NewGuid():N}");
    private ISecretProtector _protector = null!;
    private readonly IPiiHasher _hasher = new StubPiiHasher();

    private string ConnectionString =>
        $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    private AppDbContext CreateContext(ICurrentCompanyContext? companyContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        return new AppDbContext(options, companyContext, piiProtector: _protector);
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_keysPath);
        var provider = DataProtectionProvider.Create(new DirectoryInfo(_keysPath));
        _protector = new PiiSecretProtector(provider);

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        Directory.Delete(_keysPath, recursive: true);
    }

    private static async Task<(long OrgUnitId, long JobPositionId, long JobGradeId)> SeedLookupsAsync(AppDbContext context, long companyId)
    {
        var orgUnit = new OrgUnit { CompanyId = companyId, Code = "OU1", NameAr = "إدارة", NameEn = "Dept" };
        var jobGrade = new JobGrade { CompanyId = companyId, Code = "JG1", NameAr = "أ", NameEn = "A", Level = 1 };
        context.OrgUnits.Add(orgUnit);
        context.JobGrades.Add(jobGrade);
        await context.SaveChangesAsync();

        var jobPosition = new JobPosition { CompanyId = companyId, Code = "JP1", NameAr = "كاشير", NameEn = "Cashier", OrgUnitId = orgUnit.Id };
        context.JobPositions.Add(jobPosition);
        await context.SaveChangesAsync();

        return (orgUnit.Id, jobPosition.Id, jobGrade.Id);
    }

    private static Employee NewEmployee(long companyId, long branchId, long orgUnitId, long jobPositionId, long jobGradeId, string code, long? userId = null) => new()
    {
        CompanyId = companyId, BranchId = branchId, Code = code, NameAr = "موظف", NameEn = "Employee",
        OrgUnitId = orgUnitId, JobPositionId = jobPositionId, JobGradeId = jobGradeId,
        HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime, UserId = userId
    };

    [Fact]
    public async Task Migration_creates_both_tables_cleanly()
    {
        await using var context = CreateContext();
        var tableNames = await context.Database.SqlQuery<string>(
            $"SELECT name AS [Value] FROM sys.tables WHERE name IN ('Employees','EmployeePersonalData')").ToListAsync();

        Assert.Equal(2, tableNames.Count);
    }

    [Fact]
    public async Task NationalIdEncrypted_is_stored_as_unreadable_ciphertext()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        long employeeId;
        await using (var context = CreateContext(company))
        {
            var (orgUnitId, jobPositionId, jobGradeId) = await SeedLookupsAsync(context, 1);
            var employee = NewEmployee(1, 1, orgUnitId, jobPositionId, jobGradeId, "E1");
            context.Employees.Add(employee);
            await context.SaveChangesAsync();
            employeeId = employee.Id;

            context.EmployeePersonalDataRows.Add(new EmployeePersonalData
            {
                CompanyId = 1, EmployeeId = employeeId,
                NationalIdEncrypted = "29001010112345", NationalIdHash = await _hasher.ComputeHashAsync("29001010112345"),
                NationalIdLast4 = "2345", BirthDate = new DateOnly(1990, 1, 1),
                Gender = Gender.Male, MaritalStatus = MaritalStatus.Single
            });
            await context.SaveChangesAsync();
        }

        await using var raw = CreateContext(company);
        var rawValue = await raw.Database.SqlQuery<string>(
            $"SELECT NationalIdEncrypted AS [Value] FROM EmployeePersonalData WHERE EmployeeId = {employeeId}").SingleAsync();

        Assert.NotEqual("29001010112345", rawValue);
        Assert.DoesNotContain("29001010112345", rawValue);

        // And it round-trips back through EF correctly.
        await using var reload = CreateContext(company);
        var reloaded = await reload.EmployeePersonalDataRows.SingleAsync(d => d.EmployeeId == employeeId);
        Assert.Equal("29001010112345", reloaded.NationalIdEncrypted);
    }

    /// <summary>
    /// Regression test for a real bug the full-suite run caught: EF Core caches the compiled model
    /// per DbContext CLR type by default, but OnModelCreating closes over the injected ISecretProtector
    /// to build EmployeePersonalData's EncryptedStringConverter. Without AppDbContext's
    /// PiiProtectorModelCacheKeyFactory (OnConfiguring), whichever AppDbContext instance builds the
    /// model FIRST in the process — here, a plain no-protector one, exactly like ~380 pre-existing
    /// tests construct — would win that converter for every later instance too, silently disabling
    /// encryption for this test's real protector. Pins the ordering: no-protector context first, then
    /// a real-protector one, encryption must still actually occur.
    /// </summary>
    [Fact]
    public async Task Encryption_still_applies_even_when_a_no_protector_context_built_the_model_first()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        await using (var noProtectorContext = new AppDbContext(options))
        {
            _ = noProtectorContext.Model; // forces OnModelCreating with the NoOp fallback protector
        }

        var company = new TestCurrentCompanyContext(companyId: 1);
        long employeeId;
        await using (var context = CreateContext(company))
        {
            var (orgUnitId, jobPositionId, jobGradeId) = await SeedLookupsAsync(context, 1);
            var employee = NewEmployee(1, 1, orgUnitId, jobPositionId, jobGradeId, "E1");
            context.Employees.Add(employee);
            await context.SaveChangesAsync();
            employeeId = employee.Id;

            context.EmployeePersonalDataRows.Add(new EmployeePersonalData
            {
                CompanyId = 1, EmployeeId = employeeId,
                NationalIdEncrypted = "29001010112345", NationalIdHash = await _hasher.ComputeHashAsync("29001010112345"),
                NationalIdLast4 = "2345", BirthDate = new DateOnly(1990, 1, 1),
                Gender = Gender.Male, MaritalStatus = MaritalStatus.Single
            });
            await context.SaveChangesAsync();
        }

        await using var raw = CreateContext(company);
        var rawValue = await raw.Database.SqlQuery<string>(
            $"SELECT NationalIdEncrypted AS [Value] FROM EmployeePersonalData WHERE EmployeeId = {employeeId}").SingleAsync();

        Assert.NotEqual("29001010112345", rawValue);
    }

    [Fact]
    public async Task NationalIdHash_is_unique_per_company()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        var hash = await _hasher.ComputeHashAsync("29001010112345");

        await using (var context = CreateContext(company))
        {
            var (orgUnitId, jobPositionId, jobGradeId) = await SeedLookupsAsync(context, 1);
            var e1 = NewEmployee(1, 1, orgUnitId, jobPositionId, jobGradeId, "E1");
            var e2 = NewEmployee(1, 1, orgUnitId, jobPositionId, jobGradeId, "E2");
            context.Employees.AddRange(e1, e2);
            await context.SaveChangesAsync();

            context.EmployeePersonalDataRows.Add(new EmployeePersonalData
            {
                CompanyId = 1, EmployeeId = e1.Id, NationalIdEncrypted = "29001010112345", NationalIdHash = hash,
                NationalIdLast4 = "2345", BirthDate = new DateOnly(1990, 1, 1), Gender = Gender.Male, MaritalStatus = MaritalStatus.Single
            });
            await context.SaveChangesAsync();

            context.EmployeePersonalDataRows.Add(new EmployeePersonalData
            {
                CompanyId = 1, EmployeeId = e2.Id, NationalIdEncrypted = "29001010112345", NationalIdHash = hash,
                NationalIdLast4 = "2345", BirthDate = new DateOnly(1990, 1, 1), Gender = Gender.Male, MaritalStatus = MaritalStatus.Single
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task UserId_is_unique_per_company()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var (orgUnitId, jobPositionId, jobGradeId) = await SeedLookupsAsync(context, 1);

        var user = new Habbak.ERP.Domain.Settings.User
        {
            Username = $"u{Guid.NewGuid():N}"[..14], Email = "test@test.local", FullName = "مستخدم",
            PasswordHash = "x", Status = Habbak.ERP.Domain.Settings.UserStatus.Active, PasswordChangedAtUtc = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.Employees.Add(NewEmployee(1, 1, orgUnitId, jobPositionId, jobGradeId, "E1", userId: user.Id));
        await context.SaveChangesAsync();

        context.Employees.Add(NewEmployee(1, 1, orgUnitId, jobPositionId, jobGradeId, "E2", userId: user.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Employee_is_visible_only_within_its_own_branch_scope()
    {
        await using (var seed = CreateContext(new TestCurrentCompanyContext(companyId: 1)))
        {
            var (orgUnitId, jobPositionId, jobGradeId) = await SeedLookupsAsync(seed, 1);
            seed.Employees.AddRange(
                NewEmployee(1, 10, orgUnitId, jobPositionId, jobGradeId, "E1"),
                NewEmployee(1, 20, orgUnitId, jobPositionId, jobGradeId, "E2"));
            await seed.SaveChangesAsync();
        }

        await using var branchScoped = CreateContext(new TestCurrentCompanyContext(companyId: 1, branchId: 10));
        var visible = await branchScoped.Employees.ToListAsync();
        Assert.Equal("E1", Assert.Single(visible).Code);

        await using var companyWide = CreateContext(new TestCurrentCompanyContext(companyId: 1));
        Assert.Equal(2, await companyWide.Employees.CountAsync());
    }

    [Fact]
    public async Task UpdateEmployeeCommandHandler_rejects_a_manager_reassignment_that_would_create_a_cycle()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        long managerId, subordinateId;
        await using (var context = CreateContext(company))
        {
            var (orgUnitId, jobPositionId, jobGradeId) = await SeedLookupsAsync(context, 1);
            var manager = NewEmployee(1, 1, orgUnitId, jobPositionId, jobGradeId, "MGR");
            context.Employees.Add(manager);
            await context.SaveChangesAsync();
            managerId = manager.Id;

            var subordinate = NewEmployee(1, 1, orgUnitId, jobPositionId, jobGradeId, "SUB");
            subordinate.ManagerId = managerId;
            context.Employees.Add(subordinate);
            await context.SaveChangesAsync();
            subordinateId = subordinate.Id;
        }

        await using var context2 = CreateContext(company);
        var managerReloaded = await context2.Employees.SingleAsync(e => e.Id == managerId);
        var handler = new UpdateEmployeeCommandHandler(context2, company);

        // Manager(managerId) -> Subordinate(subordinateId) already exists; reassigning the manager's
        // own ManagerId to the subordinate would close the loop.
        var command = new UpdateEmployeeCommand(
            managerId, "مدير", "Manager", managerReloaded.BranchId!.Value, managerReloaded.OrgUnitId, managerReloaded.JobPositionId, managerReloaded.JobGradeId,
            ManagerId: subordinateId, UserId: null, new DateOnly(2026, 1, 1), EmploymentType.FullTime, null, true);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("HR-EMPLOYEE-MANAGER-CYCLE", ex.Code);
    }
}
