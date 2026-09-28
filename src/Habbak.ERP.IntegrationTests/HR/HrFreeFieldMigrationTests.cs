using Habbak.ERP.Application.HR.Migration;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.FixedAssets;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests.HR;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.2, sub-batches 1.2.2/1.2.3/1.2.6 — the
/// HrFreeFieldMigration schema change plus the diagnostic report/auto-match it enables. Covers the two
/// mandatory checks from 1.2.6: the old CustodyRegister.EmployeeId is untouched by the migration, and
/// EmployeeIdLinked starts out null everywhere.
///
/// No <see cref="Habbak.ERP.Application.Common.Interfaces.ICurrentCompanyContext"/> is supplied to
/// these contexts (same as HrCoreLookupsBatch1Tests), so every company-scoped entity used here is
/// created with CompanyId = null to match AppDbContext.CurrentCompanyId's null default — otherwise the
/// global query filter (CompanyId == CurrentCompanyId) would silently hide everything.
/// </summary>
public sealed class HrFreeFieldMigrationTests : IAsyncLifetime
{
    private readonly string _databaseName = $"HabbakErpTests_HrFreeField_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        return new AppDbContext(options);
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

    private static Employee NewEmployee(long orgUnitId, long jobPositionId, long jobGradeId, string code, string nameAr, string nameEn) => new()
    {
        CompanyId = null, BranchId = 1, Code = code, NameAr = nameAr, NameEn = nameEn,
        OrgUnitId = orgUnitId, JobPositionId = jobPositionId, JobGradeId = jobGradeId,
        HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime
    };

    private async Task<(long OrgUnitId, long JobPositionId, long JobGradeId)> SeedOrgScaffoldAsync(AppDbContext db)
    {
        var orgUnit = new OrgUnit { CompanyId = null, Code = $"OU{Guid.NewGuid():N}"[..8], NameAr = "إدارة", NameEn = "Dept" };
        var jobGrade = new JobGrade { CompanyId = null, Code = $"JG{Guid.NewGuid():N}"[..8], NameAr = "أ", NameEn = "A", Level = 1 };
        db.OrgUnits.Add(orgUnit);
        db.JobGrades.Add(jobGrade);
        await db.SaveChangesAsync();

        var jobPosition = new JobPosition { CompanyId = null, Code = $"JP{Guid.NewGuid():N}"[..8], NameAr = "فني", NameEn = "Technician", OrgUnitId = orgUnit.Id };
        db.JobPositions.Add(jobPosition);
        await db.SaveChangesAsync();

        return (orgUnit.Id, jobPosition.Id, jobGrade.Id);
    }

    private async Task<long> SeedMaintenanceScheduleTargetAsync(AppDbContext db, string technicianName)
    {
        var account = new Account { CompanyId = null, Code = $"AC{Guid.NewGuid():N}"[..8], NameAr = "حساب", NameEn = "Account", AccountType = AccountType.Expense, Nature = AccountNature.Debit, IsPostable = true };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var category = new FixedAssetCategory
        {
            CompanyId = null, Code = $"FC{Guid.NewGuid():N}"[..8], NameAr = "فئة", NameEn = "Category",
            AssetAccountId = account.Id, AccumulatedDepreciationAccountId = account.Id,
            DepreciationExpenseAccountId = account.Id, MaintenanceExpenseAccountId = account.Id
        };
        db.FixedAssetCategories.Add(category);
        await db.SaveChangesAsync();

        var asset = new FixedAsset
        {
            CompanyId = null, AssetNumber = $"FA{Guid.NewGuid():N}"[..8], NameAr = "أصل", NameEn = "Asset",
            CategoryId = category.Id, AcquisitionDate = new DateOnly(2026, 1, 1), CurrencyCode = "EGP",
            DepreciationStartDate = new DateOnly(2026, 1, 1)
        };
        db.FixedAssets.Add(asset);

        var maintenanceCategory = new MaintenanceCategory { CompanyId = null, Code = $"MC{Guid.NewGuid():N}"[..8], NameAr = "صيانة", NameEn = "Maintenance" };
        db.MaintenanceCategories.Add(maintenanceCategory);
        await db.SaveChangesAsync();

        var schedule = new MaintenanceSchedule
        {
            CompanyId = null, FixedAssetId = asset.Id, MaintenanceCategoryId = maintenanceCategory.Id,
            NextDueDate = new DateOnly(2026, 2, 1), TechnicianName = technicianName
        };
        db.MaintenanceSchedules.Add(schedule);
        await db.SaveChangesAsync();
        return schedule.Id;
    }

    [Fact]
    public async Task Migration_leaves_the_old_CustodyRegister_EmployeeId_untouched_and_EmployeeIdLinked_starts_null()
    {
        await using var db = CreateContext();
        var register = new CustodyRegister { CompanyId = null, EmployeeId = 1, Amount = 500m, IssueDate = new DateOnly(2026, 1, 1) };
        db.CustodyRegisters.Add(register);
        await db.SaveChangesAsync();

        await using var reloaded = CreateContext();
        var fromDb = await reloaded.CustodyRegisters.AsNoTracking().SingleAsync(r => r.Id == register.Id);

        Assert.Equal(1, fromDb.EmployeeId);
        Assert.Null(fromDb.EmployeeIdLinked);
    }

    [Fact]
    public async Task Diagnostic_report_finds_an_exact_certain_match_by_name_and_auto_match_links_it()
    {
        await using var db = CreateContext();
        var (orgUnitId, jobPositionId, jobGradeId) = await SeedOrgScaffoldAsync(db);
        var employee = NewEmployee(orgUnitId, jobPositionId, jobGradeId, "E1", "أحمد محمد", "Ahmed Mohamed");
        db.Employees.Add(employee);
        var officer = new CustodyOfficer { CompanyId = null, Code = "CO-01", NameAr = "أحمد محمد", NameEn = "Ahmed Mohamed", IsActive = true };
        db.CustodyOfficers.Add(officer);
        await db.SaveChangesAsync();

        var report = await new GetFreeFieldDiagnosticReportQueryHandler(db).Handle(new GetFreeFieldDiagnosticReportQuery(), default);
        var candidate = Assert.Single(report.CustodyOfficers);
        Assert.Equal(FreeFieldMatchStatus.ExactMatch, candidate.MatchStatus);
        Assert.Equal(employee.Id, candidate.MatchedEmployeeId);

        var result = await new ApplyFreeFieldAutoMatchCommandHandler(db).Handle(new ApplyFreeFieldAutoMatchCommand(), default);
        Assert.Equal(1, result.CustodyOfficersLinked);

        await using var reloaded = CreateContext();
        var linkedOfficer = await reloaded.CustodyOfficers.AsNoTracking().SingleAsync(o => o.Id == officer.Id);
        Assert.Equal(employee.Id, linkedOfficer.EmployeeId);
    }

    [Fact]
    public async Task Diagnostic_report_links_a_maintenance_schedule_technician_by_the_same_exact_match_rule()
    {
        await using var db = CreateContext();
        var (orgUnitId, jobPositionId, jobGradeId) = await SeedOrgScaffoldAsync(db);
        var employee = NewEmployee(orgUnitId, jobPositionId, jobGradeId, "E2", "محمود سعيد", "Mahmoud Saeed");
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        var scheduleId = await SeedMaintenanceScheduleTargetAsync(db, "محمود سعيد");

        var report = await new GetFreeFieldDiagnosticReportQueryHandler(db).Handle(new GetFreeFieldDiagnosticReportQuery(), default);
        var candidate = Assert.Single(report.MaintenanceSchedules);
        Assert.Equal(FreeFieldMatchStatus.ExactMatch, candidate.MatchStatus);

        var result = await new ApplyFreeFieldAutoMatchCommandHandler(db).Handle(new ApplyFreeFieldAutoMatchCommand(), default);
        Assert.Equal(1, result.MaintenanceSchedulesLinked);

        await using var reloaded = CreateContext();
        var linkedSchedule = await reloaded.MaintenanceSchedules.AsNoTracking().SingleAsync(s => s.Id == scheduleId);
        Assert.Equal(employee.Id, linkedSchedule.TechnicianId);
    }

    [Fact]
    public async Task Diagnostic_report_flags_two_employees_with_the_same_name_as_ambiguous_and_auto_match_skips_it()
    {
        await using var db = CreateContext();
        var (orgUnitId, jobPositionId, jobGradeId) = await SeedOrgScaffoldAsync(db);
        db.Employees.Add(NewEmployee(orgUnitId, jobPositionId, jobGradeId, "E3", "محمود علي", "Mahmoud Ali"));
        db.Employees.Add(NewEmployee(orgUnitId, jobPositionId, jobGradeId, "E4", "محمود علي", "Mahmoud Ali"));
        var officer = new CustodyOfficer { CompanyId = null, Code = "CO-03", NameAr = "محمود علي", NameEn = "Mahmoud Ali", IsActive = true };
        db.CustodyOfficers.Add(officer);
        await db.SaveChangesAsync();

        var report = await new GetFreeFieldDiagnosticReportQueryHandler(db).Handle(new GetFreeFieldDiagnosticReportQuery(), default);
        var candidate = Assert.Single(report.CustodyOfficers);
        Assert.Equal(FreeFieldMatchStatus.Ambiguous, candidate.MatchStatus);

        await new ApplyFreeFieldAutoMatchCommandHandler(db).Handle(new ApplyFreeFieldAutoMatchCommand(), default);

        await using var reloaded = CreateContext();
        var untouched = await reloaded.CustodyOfficers.AsNoTracking().SingleAsync(o => o.Id == officer.Id);
        Assert.Null(untouched.EmployeeId);
    }

    [Fact]
    public async Task Diagnostic_report_reports_no_match_when_no_employee_name_lines_up()
    {
        await using var db = CreateContext();
        var officer = new CustodyOfficer { CompanyId = null, Code = "CO-04", NameAr = "غير موجود", NameEn = "Nobody", IsActive = true };
        db.CustodyOfficers.Add(officer);
        await db.SaveChangesAsync();

        var report = await new GetFreeFieldDiagnosticReportQueryHandler(db).Handle(new GetFreeFieldDiagnosticReportQuery(), default);
        var candidate = Assert.Single(report.CustodyOfficers);
        Assert.Equal(FreeFieldMatchStatus.NoMatch, candidate.MatchStatus);
        Assert.Null(candidate.MatchedEmployeeId);
    }

    [Fact]
    public async Task Auto_match_is_idempotent_and_already_linked_records_are_reported_as_such()
    {
        await using var db = CreateContext();
        var (orgUnitId, jobPositionId, jobGradeId) = await SeedOrgScaffoldAsync(db);
        var employee = NewEmployee(orgUnitId, jobPositionId, jobGradeId, "E5", "سارة خالد", "Sara Khaled");
        db.Employees.Add(employee);
        var officer = new CustodyOfficer { CompanyId = null, Code = "CO-05", NameAr = "سارة خالد", NameEn = "Sara Khaled", IsActive = true };
        db.CustodyOfficers.Add(officer);
        await db.SaveChangesAsync();

        var first = await new ApplyFreeFieldAutoMatchCommandHandler(db).Handle(new ApplyFreeFieldAutoMatchCommand(), default);
        Assert.Equal(1, first.CustodyOfficersLinked);

        await using var db2 = CreateContext();
        var second = await new ApplyFreeFieldAutoMatchCommandHandler(db2).Handle(new ApplyFreeFieldAutoMatchCommand(), default);
        Assert.Equal(0, second.CustodyOfficersLinked);

        await using var db3 = CreateContext();
        var report = await new GetFreeFieldDiagnosticReportQueryHandler(db3).Handle(new GetFreeFieldDiagnosticReportQuery(), default);
        var candidate = Assert.Single(report.CustodyOfficers);
        Assert.Equal(FreeFieldMatchStatus.AlreadyLinked, candidate.MatchStatus);
    }
}
