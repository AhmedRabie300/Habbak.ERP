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
/// Docs/Implementation/HR-Core-Plan.md §1.1/§1.1b, Batch B5 — CRUD-over-HTTP for EmployeesController
/// and the PII reveal endpoint's permission gate + mandatory audit trail.
/// </summary>
public class EmployeeApiTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
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

        var company = new Company { Code = $"H{Guid.NewGuid():N}"[..12], NameAr = "شركة الهباك", NameEn = "Habbak Co", BaseCurrencyId = currency.Id, IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        await Habbak.ERP.Application.Settings.CompanySecurityDefaults.AddAsync(db, company.Id, DateTime.UtcNow, 0, default);
        await db.SaveChangesAsync();

        var roles = await db.Roles.IgnoreQueryFilters().Where(r => r.CompanyId == company.Id).ToDictionaryAsync(r => r.Code, r => r.Id);
        return new Seeded(company.Id, roles);
    }

    private async Task<(long OrgUnitId, long JobPositionId, long JobGradeId)> SeedLookupsAsync(long companyId)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        var orgUnit = new OrgUnit { CompanyId = companyId, Code = $"OU{Guid.NewGuid():N}"[..8], NameAr = "إدارة", NameEn = "Dept" };
        var jobGrade = new JobGrade { CompanyId = companyId, Code = $"JG{Guid.NewGuid():N}"[..8], NameAr = "أ", NameEn = "A", Level = 1 };
        db.OrgUnits.Add(orgUnit);
        db.JobGrades.Add(jobGrade);
        await db.SaveChangesAsync();

        var jobPosition = new JobPosition { CompanyId = companyId, Code = $"JP{Guid.NewGuid():N}"[..8], NameAr = "كاشير", NameEn = "Cashier", OrgUnitId = orgUnit.Id };
        db.JobPositions.Add(jobPosition);
        await db.SaveChangesAsync();

        return (orgUnit.Id, jobPosition.Id, jobGrade.Id);
    }

    private HttpClient Client(long companyId, long userId = 1, string? roles = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        if (roles is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        return client;
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()!;

    [Fact]
    public async Task Employees_full_crud_over_http()
    {
        var company = await SeedCompanyAsync();
        var (orgUnitId, jobPositionId, jobGradeId) = await SeedLookupsAsync(company.CompanyId);
        var admin = Client(company.CompanyId);

        var create = await admin.PostAsJsonAsync("/api/v1/hr/employees", new
        {
            code = $"E{Guid.NewGuid():N}"[..8], nameAr = "موظف", nameEn = "Employee", branchId = 1,
            orgUnitId, jobPositionId, jobGradeId, managerId = (long?)null, userId = (long?)null,
            hireDate = "2026-01-01", employmentType = EmploymentType.FullTime, costCenterDimensionValueId = (long?)null
        });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var getById = await admin.GetAsync($"/api/v1/hr/employees/{id}");
        getById.EnsureSuccessStatusCode();
        Assert.Equal("Employee", (await getById.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("nameEn").GetString());

        var list = await admin.GetAsync("/api/v1/hr/employees");
        list.EnsureSuccessStatusCode();

        var update = await admin.PutAsJsonAsync($"/api/v1/hr/employees/{id}", new
        {
            nameAr = "موظف٢", nameEn = "Employee II", branchId = 1, orgUnitId, jobPositionId, jobGradeId,
            managerId = (long?)null, userId = (long?)null, hireDate = "2026-01-01", employmentType = EmploymentType.FullTime,
            costCenterDimensionValueId = (long?)null, isActive = true
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var delete = await admin.DeleteAsync($"/api/v1/hr/employees/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/hr/employees/{id}")).StatusCode);
    }

    [Fact]
    public async Task Employees_are_not_readable_without_the_screens_permission_unlike_the_B4_lookups()
    {
        var company = await SeedCompanyAsync();
        var cashier = Client(company.CompanyId, 5, SystemRoles.Cashier);

        var denied = await cashier.GetAsync("/api/v1/hr/employees");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("PERMISSION-DENIED", await ErrorCodeAsync(denied));
    }

    [Fact]
    public async Task Reveal_pii_is_forbidden_without_the_button_permission_and_audited_on_every_success_with_no_batching()
    {
        var company = await SeedCompanyAsync();
        var (orgUnitId, jobPositionId, jobGradeId) = await SeedLookupsAsync(company.CompanyId);
        var admin = Client(company.CompanyId);

        var createEmployee = await admin.PostAsJsonAsync("/api/v1/hr/employees", new
        {
            code = $"E{Guid.NewGuid():N}"[..8], nameAr = "موظف", nameEn = "Employee", branchId = 1,
            orgUnitId, jobPositionId, jobGradeId, managerId = (long?)null, userId = (long?)null,
            hireDate = "2026-01-01", employmentType = EmploymentType.FullTime, costCenterDimensionValueId = (long?)null
        });
        createEmployee.EnsureSuccessStatusCode();
        var employeeId = (await createEmployee.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var createPersonalData = await admin.PostAsJsonAsync($"/api/v1/hr/employees/{employeeId}/personal-data", new
        {
            nationalId = "29001010112345", bankIban = (string?)null, bankName = (string?)null, bankId = (long?)null,
            nationalityId = (long?)null, cityId = (long?)null, militaryStatusId = (long?)null, qualificationTypeId = (long?)null,
            birthDate = "1990-01-01", gender = Gender.Male, maritalStatus = MaritalStatus.Single,
            address = (string?)null, phoneNumber = (string?)null, personalEmail = (string?)null,
            emergencyContactName = (string?)null, emergencyContactPhone = (string?)null, emergencyContactRelationshipTypeId = (long?)null
        });
        createPersonalData.EnsureSuccessStatusCode();
        var personalDataId = (await createPersonalData.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var revealRequest = new { entityType = "EmployeePersonalData", entityId = personalDataId, fieldName = "NationalIdEncrypted" };

        // Admin has full access by default (Client() default) but the button permission is still a
        // real, separate row — a fresh company only has it for roles the seeding explicitly grants.
        // A low-privileged role never gets it implicitly through its own screen View/Edit rights.
        var cashier = Client(company.CompanyId, 5, SystemRoles.Cashier);
        var deniedForCashier = await cashier.PostAsJsonAsync("/api/v1/hr/pii/reveal", revealRequest);
        Assert.Equal(HttpStatusCode.Forbidden, deniedForCashier.StatusCode);

        var grant = await admin.PutAsJsonAsync(
            $"/api/v1/settings/roles/{company.Roles[SystemRoles.Cashier]}/screens",
            new[] { new { screenCode = "HR_EMPLOYEES", canView = true, canAdd = false, canEdit = false, canDelete = false, canPrint = false, canExport = false, canApprove = true } });
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);

        var firstReveal = await cashier.PostAsJsonAsync("/api/v1/hr/pii/reveal", revealRequest);
        firstReveal.EnsureSuccessStatusCode();
        Assert.Equal("29001010112345", (await firstReveal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("value").GetString());

        var secondReveal = await cashier.PostAsJsonAsync("/api/v1/hr/pii/reveal", revealRequest);
        secondReveal.EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(company.CompanyId);
        var auditRows = await db.AuditLogs.IgnoreQueryFilters()
            .Where(a => a.EntityType == "EmployeePersonalData" && a.EntityId == personalDataId && a.FieldName == "NationalIdEncrypted")
            .ToListAsync();
        Assert.Equal(2, auditRows.Count); // one row per reveal, never batched/deduplicated
        Assert.All(auditRows, a => Assert.Null(a.NewValue)); // the revealed value itself is never written to the audit trail
    }
}
