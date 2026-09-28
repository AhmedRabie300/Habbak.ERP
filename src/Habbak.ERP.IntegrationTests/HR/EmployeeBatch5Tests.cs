using System.Security.Cryptography;
using System.Text;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.Employees.Commands.ActivateEmployee;
using Habbak.ERP.Application.HR.Employees.Commands.AssignUserToEmployee;
using Habbak.ERP.Application.HR.Employees.Commands.SetEmployeeManager;
using Habbak.ERP.Application.HR.Employees.Commands.TerminateEmployee;
using Habbak.ERP.Application.HR.Employees.Commands.UpdateEmployeePersonalData;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.FixedAssets;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Persistence;
using Habbak.ERP.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests.HR;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1/§1.1b, Batch B5 — the Employee lifecycle commands
/// (Activate/Terminate/AssignUser/SetManager) and EmployeePersonalData update. Reuses the real
/// PiiSecretProtector + ephemeral DataProtectionProvider pattern from Batch B2 (EmployeeBatch2Tests)
/// so the encryption round-trip is genuinely exercised, not stubbed.
/// </summary>
public sealed class EmployeeBatch5Tests : IAsyncLifetime
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

    private readonly string _databaseName = $"HabbakErpTests_HrB5_{Guid.NewGuid():N}";
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
        var orgUnit = new OrgUnit { CompanyId = companyId, Code = $"OU{Guid.NewGuid():N}"[..8], NameAr = "إدارة", NameEn = "Dept" };
        var jobGrade = new JobGrade { CompanyId = companyId, Code = $"JG{Guid.NewGuid():N}"[..8], NameAr = "أ", NameEn = "A", Level = 1 };
        context.OrgUnits.Add(orgUnit);
        context.JobGrades.Add(jobGrade);
        await context.SaveChangesAsync();

        var jobPosition = new JobPosition { CompanyId = companyId, Code = $"JP{Guid.NewGuid():N}"[..8], NameAr = "كاشير", NameEn = "Cashier", OrgUnitId = orgUnit.Id };
        context.JobPositions.Add(jobPosition);
        await context.SaveChangesAsync();

        return (orgUnit.Id, jobPosition.Id, jobGrade.Id);
    }

    private static async Task<long> SeedEmployeeAsync(AppDbContext context, long companyId, string code, long? userId = null)
    {
        var (orgUnitId, jobPositionId, jobGradeId) = await SeedLookupsAsync(context, companyId);
        var employee = new Employee
        {
            CompanyId = companyId, BranchId = 1, Code = code, NameAr = "موظف", NameEn = "Employee",
            OrgUnitId = orgUnitId, JobPositionId = jobPositionId, JobGradeId = jobGradeId,
            HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime, UserId = userId
        };
        context.Employees.Add(employee);
        await context.SaveChangesAsync();
        return employee.Id;
    }

    private static async Task<long> SeedUserAsync(AppDbContext context, long companyId)
    {
        var user = new User
        {
            Username = $"u{Guid.NewGuid():N}"[..14], Email = "t@test.local", FullName = "مستخدم",
            PasswordHash = "x", Status = UserStatus.Active, PasswordChangedAtUtc = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.UserScopes.Add(new UserScope
        {
            UserId = user.Id, CompanyId = companyId, RoleInScope = "CASHIER", IsDefault = true, IsActive = true, GrantedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>Phase 1.4 — a FixedAsset in the given status, in custody of a CustodyOfficer linked to this employee.</summary>
    private static async Task<long> SeedFixedAssetInCustodyAsync(AppDbContext context, long companyId, long employeeId, FixedAssetStatus status, string assetNumber)
    {
        var account = new Account
        {
            CompanyId = companyId, Code = $"AC{Guid.NewGuid():N}"[..8], NameAr = "حساب", NameEn = "Account",
            AccountType = AccountType.Expense, Nature = AccountNature.Debit, IsPostable = true, IsActive = true
        };
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        var category = new FixedAssetCategory
        {
            CompanyId = companyId, Code = $"FC{Guid.NewGuid():N}"[..8], NameAr = "فئة", NameEn = "Category",
            AssetAccountId = account.Id, AccumulatedDepreciationAccountId = account.Id,
            DepreciationExpenseAccountId = account.Id, MaintenanceExpenseAccountId = account.Id
        };
        context.FixedAssetCategories.Add(category);
        await context.SaveChangesAsync();

        var officer = new CustodyOfficer
        {
            CompanyId = companyId, Code = $"CO{Guid.NewGuid():N}"[..8], NameAr = "مسؤول عهدة", NameEn = "Custody Officer",
            IsActive = true, EmployeeId = employeeId
        };
        context.CustodyOfficers.Add(officer);
        await context.SaveChangesAsync();

        var asset = new FixedAsset
        {
            CompanyId = companyId, AssetNumber = assetNumber, NameAr = "أصل", NameEn = "Asset",
            CategoryId = category.Id, AcquisitionDate = new DateOnly(2026, 1, 1), CurrencyCode = "EGP",
            DepreciationStartDate = new DateOnly(2026, 1, 1), CustodyOfficerId = officer.Id, Status = status
        };
        context.FixedAssets.Add(asset);
        await context.SaveChangesAsync();
        return asset.Id;
    }

    // ------------------------------------------------------------------ UpdateEmployeePersonalData

    [Fact]
    public async Task UpdateEmployeePersonalData_re_encrypts_and_rejects_a_national_id_already_used_by_another_employee()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        long employeeAId, employeeBId;
        await using (var context = CreateContext(company))
        {
            employeeAId = await SeedEmployeeAsync(context, 1, "E1");
            employeeBId = await SeedEmployeeAsync(context, 1, "E2");

            context.EmployeePersonalDataRows.Add(new EmployeePersonalData
            {
                CompanyId = 1, EmployeeId = employeeAId, NationalIdEncrypted = "29001010112345",
                NationalIdHash = await _hasher.ComputeHashAsync("29001010112345"), NationalIdLast4 = "2345",
                BirthDate = new DateOnly(1990, 1, 1), Gender = Gender.Male, MaritalStatus = MaritalStatus.Single
            });
            context.EmployeePersonalDataRows.Add(new EmployeePersonalData
            {
                CompanyId = 1, EmployeeId = employeeBId, NationalIdEncrypted = "29002020212345",
                NationalIdHash = await _hasher.ComputeHashAsync("29002020212345"), NationalIdLast4 = "2345",
                BirthDate = new DateOnly(1990, 1, 1), Gender = Gender.Male, MaritalStatus = MaritalStatus.Single
            });
            await context.SaveChangesAsync();
        }

        // Re-saving E1's own (unchanged) National ID must NOT collide with itself.
        await using (var context = CreateContext(company))
        {
            var handler = new UpdateEmployeePersonalDataCommandHandler(context, company, _hasher);
            await handler.Handle(new UpdateEmployeePersonalDataCommand(
                employeeAId, "29001010112345", null, null, null, null, null, null, null,
                new DateOnly(1990, 1, 1), Gender.Male, MaritalStatus.Single, "القاهرة", null, null, null, null, null), CancellationToken.None);

            // But adopting E2's National ID must fail.
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(new UpdateEmployeePersonalDataCommand(
                employeeAId, "29002020212345", null, null, null, null, null, null, null,
                new DateOnly(1990, 1, 1), Gender.Male, MaritalStatus.Single, null, null, null, null, null, null), CancellationToken.None));
            Assert.Equal("HR-NATIONAL-ID-ALREADY-EXISTS", ex.Code);
        }

        await using var raw = CreateContext(company);
        var rawValue = await raw.Database.SqlQuery<string>(
            $"SELECT NationalIdEncrypted AS [Value] FROM EmployeePersonalData WHERE EmployeeId = {employeeAId}").SingleAsync();
        Assert.DoesNotContain("29001010112345", rawValue);

        var reloaded = await raw.EmployeePersonalDataRows.SingleAsync(d => d.EmployeeId == employeeAId);
        Assert.Equal("29001010112345", reloaded.NationalIdEncrypted);
        Assert.Equal("القاهرة", reloaded.Address);
    }

    // ------------------------------------------------------------------ AssignUserToEmployee

    [Fact]
    public async Task AssignUserToEmployee_updates_UserId_and_rejects_a_user_already_linked_to_another_employee()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeAId = await SeedEmployeeAsync(context, 1, "E1");
        var employeeBId = await SeedEmployeeAsync(context, 1, "E2");
        var userId = await SeedUserAsync(context, 1);

        var handler = new AssignUserToEmployeeCommandHandler(context, company);
        await handler.Handle(new AssignUserToEmployeeCommand(employeeAId, userId), CancellationToken.None);

        var employeeA = await context.Employees.SingleAsync(e => e.Id == employeeAId);
        Assert.Equal(userId, employeeA.UserId);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(new AssignUserToEmployeeCommand(employeeBId, userId), CancellationToken.None));
        Assert.Equal("HR-USER-ALREADY-LINKED", ex.Code);
    }

    // ------------------------------------------------------------------ SetEmployeeManager

    [Fact]
    public async Task SetEmployeeManager_updates_ManagerId_and_rejects_a_cycle()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var xId = await SeedEmployeeAsync(context, 1, "X");
        var yId = await SeedEmployeeAsync(context, 1, "Y");

        var handler = new SetEmployeeManagerCommandHandler(context, company);

        // X manages Y.
        await handler.Handle(new SetEmployeeManagerCommand(yId, xId), CancellationToken.None);
        Assert.Equal(xId, (await context.Employees.SingleAsync(e => e.Id == yId)).ManagerId);

        // Y managing X back would close the loop.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(new SetEmployeeManagerCommand(xId, yId), CancellationToken.None));
        Assert.Equal("HR-EMPLOYEE-MANAGER-CYCLE", ex.Code);
    }

    // ------------------------------------------------------------------ ActivateEmployee

    [Fact]
    public async Task ActivateEmployee_fails_when_mandatory_documents_are_missing_and_succeeds_once_provided()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");

        context.EmploymentContracts.Add(new EmploymentContract
        {
            CompanyId = 1, BranchId = 1, EmployeeId = employeeId, ContractType = ContractType.Indefinite,
            StartDate = new DateOnly(2026, 1, 1), BasicSalary = 5000m, InsurableWage = 4500m, WorkingHoursPerDay = 8,
            Status = EmploymentContractStatus.Active
        });
        var mandatoryDocType = new EmployeeDocumentType { CompanyId = 1, Code = "ID", NameAr = "بطاقة", NameEn = "ID Card", IsMandatory = true };
        context.EmployeeDocumentTypes.Add(mandatoryDocType);
        await context.SaveChangesAsync();

        var handler = new ActivateEmployeeCommandHandler(context, company);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(new ActivateEmployeeCommand(employeeId), CancellationToken.None));
        Assert.Equal("HR-EMPLOYEE-MISSING-MANDATORY-DOCUMENTS", ex.Code);

        var attachment = new Habbak.ERP.Domain.Common.Attachment
        {
            CompanyId = 1, EntityType = "EmployeeDocument", EntityId = 0, FileName = "id.pdf", ContentType = "application/pdf", FileSizeBytes = 1, Content = [1]
        };
        context.Attachments.Add(attachment);
        await context.SaveChangesAsync();
        context.EmployeeDocuments.Add(new EmployeeDocument
        {
            CompanyId = 1, BranchId = 1, EmployeeId = employeeId, EmployeeDocumentTypeId = mandatoryDocType.Id,
            AttachmentId = attachment.Id, IssueDate = new DateOnly(2026, 1, 1)
        });
        await context.SaveChangesAsync();

        await handler.Handle(new ActivateEmployeeCommand(employeeId), CancellationToken.None);
        Assert.Equal(EmployeeStatus.Active, (await context.Employees.SingleAsync(e => e.Id == employeeId)).Status);
    }

    // ------------------------------------------------------------------ TerminateEmployee

    [Fact]
    public async Task TerminateEmployee_is_rejected_while_an_open_custody_exists()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");

        context.CustodyRegisters.Add(new CustodyRegister
        {
            CompanyId = 1, EmployeeId = employeeId, Amount = 1000m, IssueDate = new DateOnly(2026, 1, 1), Status = CustodyStatus.Open
        });
        await context.SaveChangesAsync();

        var handler = new TerminateEmployeeCommandHandler(context, company);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(new TerminateEmployeeCommand(employeeId), CancellationToken.None));
        Assert.Equal("HR-EMPLOYEE-OPEN-CUSTODY", ex.Code);
    }

    /// <summary>Phase 1.4 — widens the same check to Employee -&gt; CustodyOfficer.EmployeeId -&gt; FixedAsset.CustodyOfficerId (rule 43).</summary>
    [Fact]
    public async Task TerminateEmployee_is_rejected_when_an_asset_is_still_in_their_custody()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");
        await SeedFixedAssetInCustodyAsync(context, 1, employeeId, FixedAssetStatus.Active, "FA-001");

        var handler = new TerminateEmployeeCommandHandler(context, company);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(new TerminateEmployeeCommand(employeeId), CancellationToken.None));
        Assert.Equal("HR-EMPLOYEE-OPEN-CUSTODY", ex.Code);
        Assert.Contains("FA-001", ex.Message);
    }

    [Fact]
    public async Task TerminateEmployee_is_not_blocked_by_a_disposed_or_written_off_or_draft_asset_in_former_custody()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");
        await SeedFixedAssetInCustodyAsync(context, 1, employeeId, FixedAssetStatus.Disposed, "FA-002");
        await SeedFixedAssetInCustodyAsync(context, 1, employeeId, FixedAssetStatus.WrittenOff, "FA-003");
        await SeedFixedAssetInCustodyAsync(context, 1, employeeId, FixedAssetStatus.Draft, "FA-004");

        var handler = new TerminateEmployeeCommandHandler(context, company);
        await handler.Handle(new TerminateEmployeeCommand(employeeId), CancellationToken.None);

        Assert.Equal(EmployeeStatus.Terminated, (await context.Employees.SingleAsync(e => e.Id == employeeId)).Status);
    }

    [Fact]
    public async Task TerminateEmployee_rejection_message_names_both_cash_and_asset_custody_when_both_are_open()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");
        context.CustodyRegisters.Add(new CustodyRegister
        {
            CompanyId = 1, EmployeeId = employeeId, Amount = 500m, IssueDate = new DateOnly(2026, 1, 1), Status = CustodyStatus.Open
        });
        await context.SaveChangesAsync();
        await SeedFixedAssetInCustodyAsync(context, 1, employeeId, FixedAssetStatus.InMaintenance, "FA-005");

        var handler = new TerminateEmployeeCommandHandler(context, company);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(new TerminateEmployeeCommand(employeeId), CancellationToken.None));
        Assert.Equal("HR-EMPLOYEE-OPEN-CUSTODY", ex.Code);
        Assert.Contains("عهدة نقدية", ex.Message);
        Assert.Contains("FA-005", ex.Message);
    }

    [Fact]
    public async Task TerminateEmployee_sets_status_suspends_the_user_and_deactivates_its_scopes_in_one_save()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var userId = await SeedUserAsync(context, 1);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1", userId);

        context.EmploymentContracts.Add(new EmploymentContract
        {
            CompanyId = 1, BranchId = 1, EmployeeId = employeeId, ContractType = ContractType.Indefinite,
            StartDate = new DateOnly(2026, 1, 1), BasicSalary = 5000m, InsurableWage = 4500m, WorkingHoursPerDay = 8,
            Status = EmploymentContractStatus.Active
        });
        await context.SaveChangesAsync();

        var handler = new TerminateEmployeeCommandHandler(context, company);
        await handler.Handle(new TerminateEmployeeCommand(employeeId), CancellationToken.None);

        var employee = await context.Employees.SingleAsync(e => e.Id == employeeId);
        Assert.Equal(EmployeeStatus.Terminated, employee.Status);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), employee.TerminationDate);

        var contract = await context.EmploymentContracts.SingleAsync(c => c.EmployeeId == employeeId);
        Assert.Equal(EmploymentContractStatus.Terminated, contract.Status);

        var user = await context.Users.SingleAsync(u => u.Id == userId);
        Assert.Equal(UserStatus.Suspended, user.Status);

        var scope = await context.UserScopes.SingleAsync(s => s.UserId == userId);
        Assert.False(scope.IsActive);

        // Terminating an already-terminated employee is rejected.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(new TerminateEmployeeCommand(employeeId), CancellationToken.None));
        Assert.Equal("HR-EMPLOYEE-ALREADY-TERMINATED", ex.Code);
    }

    [Fact]
    public async Task TerminateEmployee_revokes_the_linked_user_active_refresh_token_session()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var userId = await SeedUserAsync(context, 1);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1", userId);

        context.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId, Token = Guid.NewGuid().ToString("N"), ExpiresAtUtc = DateTime.UtcNow.AddDays(7), CreatedByIp = "10.0.0.1"
        });
        await context.SaveChangesAsync();

        var handler = new TerminateEmployeeCommandHandler(context, company);
        await handler.Handle(new TerminateEmployeeCommand(employeeId), CancellationToken.None);

        var token = await context.RefreshTokens.SingleAsync(t => t.UserId == userId);
        Assert.False(token.IsActive);
        Assert.NotNull(token.RevokedAtUtc);
        Assert.Equal("EmployeeTerminated", token.RevokedReason);
    }
}
