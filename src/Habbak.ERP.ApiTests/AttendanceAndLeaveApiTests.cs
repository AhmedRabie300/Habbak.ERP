using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habbak.ERP.API.Auth;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 3, Sub-Batch 3.7 — CRUD-over-HTTP لكيان تمثيلي
/// (WorkShiftDefinitions، نفس منطق HrLookupsApiTests: باقي الـLookups بنفس الشكل بالظبط)، تطبيق
/// صلاحيات الشاشة على Controller غير-Lookup (Attendance)، ودورة LeaveRequest كاملة عن طريق HTTP
/// (تكمّل الاختبارات الأعمق في IntegrationTests/Attendance/AttendanceAndLeaveTests.cs بمستوى HTTP
/// مش استدعاء الـHandler مباشرة)، ونطاق Self (GetMyAttendance).
/// </summary>
public class AttendanceAndLeaveApiTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static readonly BcryptPasswordHasher Hasher = new();
    private const string Password = "Strong@123";

    private sealed record Seeded(long CompanyId, Dictionary<string, long> Roles);

    private async Task<Seeded> SeedCompanyAsync()
    {
        await using var db = factory.CreateDirectDbContext(0);
        var currency = await db.Currencies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Code == "EGP");
        if (currency is null)
        {
            currency = new Currency { Code = "EGP", NameAr = "جنيه مصري", NameEn = "Egyptian Pound", IsActive = true };
            db.Currencies.Add(currency);
            await db.SaveChangesAsync();
        }

        if (!await db.MenuItems.AnyAsync())
        {
            db.MenuItems.AddRange(Habbak.ERP.Infrastructure.Persistence.Seeding.MenuItemSeedData.Build());
            await db.SaveChangesAsync();
        }

        if (!await db.Screens.AnyAsync())
        {
            db.Screens.AddRange(Habbak.ERP.Infrastructure.Persistence.Seeding.ScreenSeedData.Build());
            await db.SaveChangesAsync();
        }

        var company = new Company { Code = $"H{Guid.NewGuid():N}"[..12], NameAr = "شركة الهباك", NameEn = "Habbak Co", BaseCurrencyId = currency.Id, IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        await Habbak.ERP.Application.Settings.CompanySecurityDefaults.AddAsync(db, company.Id, DateTime.UtcNow, 0, default);
        await db.SaveChangesAsync();

        var roles = await db.Roles.IgnoreQueryFilters().Where(r => r.CompanyId == company.Id).ToDictionaryAsync(r => r.Code, r => r.Id);
        return new Seeded(company.Id, roles);
    }

    private async Task<long> SeedUserAsync(Seeded company, string role)
    {
        await using var db = factory.CreateDirectDbContext(company.CompanyId);
        var username = $"u{Guid.NewGuid():N}"[..14];
        var user = new User
        {
            Username = username, Email = $"{username}@test.local", FullName = "مستخدم", PasswordHash = Hasher.Hash(Password),
            Status = UserStatus.Active, PasswordChangedAtUtc = DateTime.UtcNow
        };
        user.UserRoles.Add(new UserRole { RoleId = company.Roles[role], AssignedAtUtc = DateTime.UtcNow, AssignedByUserId = 0 });
        user.UserScopes.Add(new UserScope { CompanyId = company.CompanyId, RoleInScope = role, IsDefault = true, IsActive = true, GrantedAtUtc = DateTime.UtcNow });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>موظف مربوط بمستخدم (Employee.UserId) — نطاق Self بيتحل منه، مش من Claim مباشر.</summary>
    private async Task<long> SeedEmployeeAsync(Seeded company, long? userId = null)
    {
        await using var db = factory.CreateDirectDbContext(company.CompanyId);
        var orgUnit = new OrgUnit { CompanyId = company.CompanyId, Code = $"OU{Guid.NewGuid():N}"[..10], NameAr = "إدارة", NameEn = "Dept" };
        db.OrgUnits.Add(orgUnit);
        var grade = new JobGrade { CompanyId = company.CompanyId, Code = $"JG{Guid.NewGuid():N}"[..10], NameAr = "أ", NameEn = "A", Level = 1 };
        db.JobGrades.Add(grade);
        await db.SaveChangesAsync();

        var jobPosition = new JobPosition { CompanyId = company.CompanyId, Code = $"JP{Guid.NewGuid():N}"[..10], NameAr = "كاشير", NameEn = "Cashier", OrgUnitId = orgUnit.Id };
        db.JobPositions.Add(jobPosition);
        await db.SaveChangesAsync();

        var employee = new Employee
        {
            CompanyId = company.CompanyId, BranchId = 1, Code = $"E{Guid.NewGuid():N}"[..10], NameAr = "موظف", NameEn = "Employee",
            OrgUnitId = orgUnit.Id, JobPositionId = jobPosition.Id, JobGradeId = grade.Id, UserId = userId,
            HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime, Status = EmployeeStatus.Active
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee.Id;
    }

    private async Task<long> SeedLeaveTypeWithBalanceAsync(Seeded company, long employeeId, int year, decimal accrued = 21)
    {
        await using var db = factory.CreateDirectDbContext(company.CompanyId);
        var leaveType = new Habbak.ERP.Domain.Attendance.LeaveType
        {
            CompanyId = company.CompanyId, Code = $"LT{Guid.NewGuid():N}"[..10], NameAr = "سنوية", NameEn = "Annual", IsActive = true,
            AccrualMethod = Habbak.ERP.Domain.Attendance.LeaveAccrualMethod.Annual, AnnualDays = 21, PaidPercentage = 100
        };
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();

        // Year لازم يطابق سنة LeaveRequest.StartDate بالظبط — LeaveBalanceHelpers.FindOrCreateAsync
        // بيدوّر بمفتاح (EmployeeId, LeaveTypeId, StartDate.Year)، مش السنة الحالية.
        db.LeaveBalances.Add(new Habbak.ERP.Domain.Attendance.LeaveBalance { CompanyId = company.CompanyId, EmployeeId = employeeId, LeaveTypeId = leaveType.Id, Year = year, AccruedThisYear = accrued });
        await db.SaveChangesAsync();
        return leaveType.Id;
    }

    private HttpClient Client(long companyId, long userId = 1, string? roles = null, long? employeeId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        if (roles is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        if (employeeId is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.EmployeeIdHeader, employeeId.Value.ToString());
        return client;
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()!;

    // --------------------------------------------------------------------- WorkShiftDefinitions (lookup)

    [Fact]
    public async Task WorkShiftDefinitions_full_crud_over_http()
    {
        var company = await SeedCompanyAsync();
        var admin = Client(company.CompanyId);

        var create = await admin.PostAsJsonAsync("/api/v1/hr/work-shifts", new
        {
            code = $"WS{Guid.NewGuid():N}"[..10], nameAr = "صباحي", nameEn = "Morning", startTime = "08:00:00", endTime = "16:00:00", breakMinutes = 30, isNightShift = false
        });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var getById = await admin.GetAsync($"/api/v1/hr/work-shifts/{id}");
        getById.EnsureSuccessStatusCode();
        Assert.Equal("Morning", (await getById.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("nameEn").GetString());

        var update = await admin.PutAsJsonAsync($"/api/v1/hr/work-shifts/{id}", new
        {
            nameAr = "صباحي٢", nameEn = "Morning II", startTime = "08:00:00", endTime = "16:00:00", breakMinutes = 45, isNightShift = false, isActive = true
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var delete = await admin.DeleteAsync($"/api/v1/hr/work-shifts/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/hr/work-shifts/{id}")).StatusCode);
    }

    // --------------------------------------------------------------------- Screen permission enforcement

    [Fact]
    public async Task Attendance_screen_is_forbidden_without_the_permission_and_allowed_after_granting_it()
    {
        var company = await SeedCompanyAsync();
        var cashierUserId = await SeedUserAsync(company, SystemRoles.Cashier);
        var cashier = Client(company.CompanyId, cashierUserId, SystemRoles.Cashier);

        var denied = await cashier.GetAsync("/api/v1/hr/attendance");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("PERMISSION-DENIED", await ErrorCodeAsync(denied));

        var grant = await Client(company.CompanyId).PutAsJsonAsync(
            $"/api/v1/settings/roles/{company.Roles[SystemRoles.Cashier]}/screens",
            new[] { new { screenCode = "HR_ATTENDANCE", canView = true, canAdd = false, canEdit = false, canDelete = false, canPrint = false, canExport = false, canApprove = false } });
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync("/api/v1/hr/attendance")).StatusCode);
    }

    // --------------------------------------------------------------------- LeaveRequest full cycle over HTTP

    [Fact]
    public async Task Leave_request_create_calculate_days_and_submit_over_http_approves_immediately()
    {
        var company = await SeedCompanyAsync();
        var employeeId = await SeedEmployeeAsync(company);
        var admin = Client(company.CompanyId);

        var startDate = new DateOnly(DateTime.UtcNow.Year + 1, 3, 10);
        var endDate = startDate.AddDays(4);
        var leaveTypeId = await SeedLeaveTypeWithBalanceAsync(company, employeeId, startDate.Year);

        var calc = await admin.GetAsync($"/api/v1/hr/leave-requests/calculate-days?employeeId={employeeId}&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");
        calc.EnsureSuccessStatusCode();
        var calcBody = await calc.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, calcBody.GetProperty("days").GetDecimal());

        var create = await admin.PostAsJsonAsync("/api/v1/hr/leave-requests", new { employeeId, leaveTypeId, startDate, endDate, reason = (string?)null });
        create.EnsureSuccessStatusCode();
        var requestId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var submit = await admin.PostAsync($"/api/v1/hr/leave-requests/{requestId}/submit", null);
        Assert.Equal(HttpStatusCode.NoContent, submit.StatusCode);

        var getById = await admin.GetAsync($"/api/v1/hr/leave-requests/{requestId}");
        getById.EnsureSuccessStatusCode();
        Assert.Equal("Approved", (await getById.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        var balances = await admin.GetAsync($"/api/v1/hr/leave-balances?employeeId={employeeId}");
        balances.EnsureSuccessStatusCode();
        var balance = (await balances.Content.ReadFromJsonAsync<JsonElement[]>())!.Single();
        Assert.Equal(5, balance.GetProperty("used").GetDecimal());
        Assert.Equal(0, balance.GetProperty("pending").GetDecimal());
    }

    // --------------------------------------------------------------------- Self scope

    [Fact]
    public async Task GetMyAttendance_only_returns_the_callers_own_linked_employee_records()
    {
        var company = await SeedCompanyAsync();
        var userA = await SeedUserAsync(company, SystemRoles.Cashier);
        var userB = await SeedUserAsync(company, SystemRoles.Cashier);
        var employeeA = await SeedEmployeeAsync(company, userA);
        var employeeB = await SeedEmployeeAsync(company, userB);

        var admin = Client(company.CompanyId);
        await using (var db = factory.CreateDirectDbContext(company.CompanyId))
        {
            db.Attendances.Add(new Habbak.ERP.Domain.Attendance.Attendance { CompanyId = company.CompanyId, BranchId = 1, EmployeeId = employeeA, WorkDate = new DateOnly(2026, 10, 5), Status = Habbak.ERP.Domain.Attendance.AttendanceStatus.Present });
            db.Attendances.Add(new Habbak.ERP.Domain.Attendance.Attendance { CompanyId = company.CompanyId, BranchId = 1, EmployeeId = employeeB, WorkDate = new DateOnly(2026, 10, 5), Status = Habbak.ERP.Domain.Attendance.AttendanceStatus.Present });
            await db.SaveChangesAsync();
        }

        // HR_ATTENDANCE مش لازمة هنا — "mine" مقصودة تتفتح لأي مستخدم موظف (Self)، بس الـController
        // نفسه لسه محتاج صلاحية الشاشة (Phase-3-Research.md §3.4 — مفيش [AnySignedInUser] هنا، Known
        // Limitation حقيقية للـSelf-Service قبل Phase 6، موثّقة في Phase-3-Final.md).
        await Client(company.CompanyId).PutAsJsonAsync(
            $"/api/v1/settings/roles/{company.Roles[SystemRoles.Cashier]}/screens",
            new[] { new { screenCode = "HR_ATTENDANCE", canView = true, canAdd = false, canEdit = false, canDelete = false, canPrint = false, canExport = false, canApprove = false } });

        var clientA = Client(company.CompanyId, userA, SystemRoles.Cashier, employeeA);
        var mine = await clientA.GetAsync("/api/v1/hr/attendance/mine");
        mine.EnsureSuccessStatusCode();
        var rows = await mine.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.Single(rows!);
        Assert.Equal(employeeA, rows![0].GetProperty("employeeId").GetInt64());
    }
}
