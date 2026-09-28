using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using Microsoft.EntityFrameworkCore;
using Habbak.ERP.Infrastructure.Persistence;
using Xunit;

namespace Habbak.ERP.IntegrationTests.HR;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B3 — EmploymentContract + EmployeeDocument +
/// EmployeeCertification, the first real use of IEmployeeScopedEntity (built in Phase 0.1, unused
/// until now). No PII/encrypted columns here, so the PiiProtectorModelCacheKeyFactory fix from B2
/// isn't exercised differently by these entities — they just need a working AppDbContext like any
/// other test, which the shared model cache (now keyed by protector identity) already guarantees.
/// </summary>
public sealed class EmploymentContractsBatch3Tests : IAsyncLifetime
{
    private readonly string _databaseName = $"HabbakErpTests_HrB3_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    private AppDbContext CreateContext(ICurrentCompanyContext? companyContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        return new AppDbContext(options, companyContext);
    }

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
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

    /// <summary>Single-employee convenience wrapper — seeds its own lookups. Tests that need more
    /// than one employee must call SeedLookupsAsync once and pass the same ids to each employee, or
    /// the second call collides on the lookups' own (CompanyId, Code) unique index.</summary>
    private static async Task<long> SeedEmployeeAsync(AppDbContext context, long companyId, long branchId, string code)
    {
        var (orgUnitId, jobPositionId, jobGradeId) = await SeedLookupsAsync(context, companyId);
        return await AddEmployeeAsync(context, companyId, branchId, orgUnitId, jobPositionId, jobGradeId, code);
    }

    private static async Task<long> AddEmployeeAsync(AppDbContext context, long companyId, long branchId, long orgUnitId, long jobPositionId, long jobGradeId, string code)
    {
        var employee = new Employee
        {
            CompanyId = companyId, BranchId = branchId, Code = code, NameAr = "موظف", NameEn = "Employee",
            OrgUnitId = orgUnitId, JobPositionId = jobPositionId, JobGradeId = jobGradeId,
            HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime
        };
        context.Employees.Add(employee);
        await context.SaveChangesAsync();
        return employee.Id;
    }

    [Fact]
    public async Task Migration_creates_all_three_tables_cleanly()
    {
        await using var context = CreateContext();
        var tableNames = await context.Database.SqlQuery<string>(
            $"SELECT name AS [Value] FROM sys.tables WHERE name IN ('EmploymentContracts','EmployeeDocuments','EmployeeCertifications')").ToListAsync();

        Assert.Equal(3, tableNames.Count);
    }

    [Fact]
    public async Task EmploymentContract_can_be_created_and_linked_to_its_employee()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, 1, "E1");

        var contract = new EmploymentContract
        {
            CompanyId = 1, BranchId = 1, EmployeeId = employeeId, ContractType = ContractType.Indefinite,
            StartDate = new DateOnly(2026, 1, 1), BasicSalary = 5000m, InsurableWage = 4500m, WorkingHoursPerDay = 8
        };
        context.EmploymentContracts.Add(contract);
        await context.SaveChangesAsync();

        Assert.True(contract.Id > 0);
        Assert.Equal(EmploymentContractStatus.Draft, contract.Status);
    }

    [Fact]
    public async Task EmploymentContract_creation_fails_for_a_nonexistent_employee()
    {
        await using var context = CreateContext(new TestCurrentCompanyContext(companyId: 1));

        context.EmploymentContracts.Add(new EmploymentContract
        {
            CompanyId = 1, BranchId = 1, EmployeeId = 999_999, ContractType = ContractType.Indefinite,
            StartDate = new DateOnly(2026, 1, 1), BasicSalary = 5000m, InsurableWage = 4500m, WorkingHoursPerDay = 8
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_an_employee_is_blocked_while_an_employment_contract_still_references_it()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        long employeeId;
        await using (var context = CreateContext(company))
        {
            employeeId = await SeedEmployeeAsync(context, 1, 1, "E1");
            context.EmploymentContracts.Add(new EmploymentContract
            {
                CompanyId = 1, BranchId = 1, EmployeeId = employeeId, ContractType = ContractType.Indefinite,
                StartDate = new DateOnly(2026, 1, 1), BasicSalary = 5000m, InsurableWage = 4500m, WorkingHoursPerDay = 8
            });
            await context.SaveChangesAsync();
        }

        await using var deleteContext = CreateContext(company);
        var employee = await deleteContext.Employees.SingleAsync(e => e.Id == employeeId);
        deleteContext.Employees.Remove(employee);

        await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());
    }

    [Fact]
    public async Task EmployeeDocument_links_an_employee_a_document_type_and_an_existing_attachment()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, 1, "E1");

        var documentType = new EmployeeDocumentType { CompanyId = 1, Code = "ID", NameAr = "بطاقة", NameEn = "ID Card" };
        context.EmployeeDocumentTypes.Add(documentType);
        var attachment = new Attachment
        {
            CompanyId = 1, EntityType = nameof(EmployeeDocument), EntityId = 0,
            FileName = "id.pdf", ContentType = "application/pdf", FileSizeBytes = 3, Content = [1, 2, 3]
        };
        context.Attachments.Add(attachment);
        await context.SaveChangesAsync();

        var document = new EmployeeDocument
        {
            CompanyId = 1, BranchId = 1, EmployeeId = employeeId, EmployeeDocumentTypeId = documentType.Id,
            AttachmentId = attachment.Id, IssueDate = new DateOnly(2026, 1, 1)
        };
        context.EmployeeDocuments.Add(document);
        await context.SaveChangesAsync();

        Assert.True(document.Id > 0);
    }

    [Fact]
    public async Task Deleting_an_employee_is_blocked_while_an_employee_document_still_references_it()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        long employeeId;
        await using (var context = CreateContext(company))
        {
            employeeId = await SeedEmployeeAsync(context, 1, 1, "E1");
            var documentType = new EmployeeDocumentType { CompanyId = 1, Code = "ID", NameAr = "بطاقة", NameEn = "ID Card" };
            context.EmployeeDocumentTypes.Add(documentType);
            var attachment = new Attachment
            {
                CompanyId = 1, EntityType = nameof(EmployeeDocument), EntityId = 0,
                FileName = "id.pdf", ContentType = "application/pdf", FileSizeBytes = 3, Content = [1, 2, 3]
            };
            context.Attachments.Add(attachment);
            await context.SaveChangesAsync();

            context.EmployeeDocuments.Add(new EmployeeDocument
            {
                CompanyId = 1, BranchId = 1, EmployeeId = employeeId, EmployeeDocumentTypeId = documentType.Id,
                AttachmentId = attachment.Id, IssueDate = new DateOnly(2026, 1, 1)
            });
            await context.SaveChangesAsync();
        }

        await using var deleteContext = CreateContext(company);
        var employee = await deleteContext.Employees.SingleAsync(e => e.Id == employeeId);
        deleteContext.Employees.Remove(employee);

        await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());
    }

    [Fact]
    public async Task EmployeeCertification_can_be_created_and_deleting_its_employee_is_blocked()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        long employeeId;
        await using (var context = CreateContext(company))
        {
            employeeId = await SeedEmployeeAsync(context, 1, 1, "E1");
            context.EmployeeCertifications.Add(new EmployeeCertification
            {
                CompanyId = 1, BranchId = 1, EmployeeId = employeeId, NameAr = "باريستا", NameEn = "Barista",
                Issuer = "SCA", IssueDate = new DateOnly(2026, 1, 1)
            });
            await context.SaveChangesAsync();
        }

        await using var readBack = CreateContext(company);
        Assert.Equal(1, await readBack.EmployeeCertifications.CountAsync());

        await using var deleteContext = CreateContext(company);
        var employee = await deleteContext.Employees.SingleAsync(e => e.Id == employeeId);
        deleteContext.Employees.Remove(employee);

        await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());
    }

    [Fact]
    public async Task IBranchScopedEntity_filter_hides_rows_from_a_different_branch()
    {
        await using (var seed = CreateContext(new TestCurrentCompanyContext(companyId: 1)))
        {
            var (orgUnitId, jobPositionId, jobGradeId) = await SeedLookupsAsync(seed, 1);
            var employeeInBranch10 = await AddEmployeeAsync(seed, 1, 10, orgUnitId, jobPositionId, jobGradeId, "E1");
            var employeeInBranch20 = await AddEmployeeAsync(seed, 1, 20, orgUnitId, jobPositionId, jobGradeId, "E2");

            seed.EmploymentContracts.AddRange(
                new EmploymentContract { CompanyId = 1, BranchId = 10, EmployeeId = employeeInBranch10, ContractType = ContractType.Indefinite, StartDate = new DateOnly(2026, 1, 1), BasicSalary = 5000m, InsurableWage = 4500m, WorkingHoursPerDay = 8 },
                new EmploymentContract { CompanyId = 1, BranchId = 20, EmployeeId = employeeInBranch20, ContractType = ContractType.Indefinite, StartDate = new DateOnly(2026, 1, 1), BasicSalary = 6000m, InsurableWage = 5500m, WorkingHoursPerDay = 8 });
            await seed.SaveChangesAsync();
        }

        await using var branchScoped = CreateContext(new TestCurrentCompanyContext(companyId: 1, branchId: 10));
        Assert.Equal(1, await branchScoped.EmploymentContracts.CountAsync());

        await using var companyWide = CreateContext(new TestCurrentCompanyContext(companyId: 1));
        Assert.Equal(2, await companyWide.EmploymentContracts.CountAsync());
    }

    [Fact]
    public async Task IEmployeeScopedEntity_filter_applies_to_all_three_entities()
    {
        long employeeA, employeeB;
        await using (var seed = CreateContext(new TestCurrentCompanyContext(companyId: 1)))
        {
            var (orgUnitId, jobPositionId, jobGradeId) = await SeedLookupsAsync(seed, 1);
            employeeA = await AddEmployeeAsync(seed, 1, 1, orgUnitId, jobPositionId, jobGradeId, "E1");
            employeeB = await AddEmployeeAsync(seed, 1, 1, orgUnitId, jobPositionId, jobGradeId, "E2");

            var documentType = new EmployeeDocumentType { CompanyId = 1, Code = "ID", NameAr = "بطاقة", NameEn = "ID Card" };
            seed.EmployeeDocumentTypes.Add(documentType);
            var attachment = new Attachment
            {
                CompanyId = 1, EntityType = nameof(EmployeeDocument), EntityId = 0,
                FileName = "id.pdf", ContentType = "application/pdf", FileSizeBytes = 3, Content = [1, 2, 3]
            };
            seed.Attachments.Add(attachment);
            await seed.SaveChangesAsync();

            seed.EmploymentContracts.AddRange(
                new EmploymentContract { CompanyId = 1, BranchId = 1, EmployeeId = employeeA, ContractType = ContractType.Indefinite, StartDate = new DateOnly(2026, 1, 1), BasicSalary = 5000m, InsurableWage = 4500m, WorkingHoursPerDay = 8 },
                new EmploymentContract { CompanyId = 1, BranchId = 1, EmployeeId = employeeB, ContractType = ContractType.Indefinite, StartDate = new DateOnly(2026, 1, 1), BasicSalary = 6000m, InsurableWage = 5500m, WorkingHoursPerDay = 8 });

            seed.EmployeeDocuments.AddRange(
                new EmployeeDocument { CompanyId = 1, BranchId = 1, EmployeeId = employeeA, EmployeeDocumentTypeId = documentType.Id, AttachmentId = attachment.Id, IssueDate = new DateOnly(2026, 1, 1) },
                new EmployeeDocument { CompanyId = 1, BranchId = 1, EmployeeId = employeeB, EmployeeDocumentTypeId = documentType.Id, AttachmentId = attachment.Id, IssueDate = new DateOnly(2026, 1, 1) });

            seed.EmployeeCertifications.AddRange(
                new EmployeeCertification { CompanyId = 1, BranchId = 1, EmployeeId = employeeA, NameAr = "باريستا", NameEn = "Barista", Issuer = "SCA", IssueDate = new DateOnly(2026, 1, 1) },
                new EmployeeCertification { CompanyId = 1, BranchId = 1, EmployeeId = employeeB, NameAr = "سلامة غذاء", NameEn = "Food Safety", Issuer = "MOH", IssueDate = new DateOnly(2026, 1, 1) });

            await seed.SaveChangesAsync();
        }

        await using var scopedToA = CreateContext(new TestCurrentCompanyContext(companyId: 1, employeeId: employeeA));
        Assert.Equal(1, await scopedToA.EmploymentContracts.CountAsync());
        Assert.Equal(1, await scopedToA.EmployeeDocuments.CountAsync());
        Assert.Equal(1, await scopedToA.EmployeeCertifications.CountAsync());

        await using var unscoped = CreateContext(new TestCurrentCompanyContext(companyId: 1));
        Assert.Equal(2, await unscoped.EmploymentContracts.CountAsync());
        Assert.Equal(2, await unscoped.EmployeeDocuments.CountAsync());
        Assert.Equal(2, await unscoped.EmployeeCertifications.CountAsync());
    }
}
