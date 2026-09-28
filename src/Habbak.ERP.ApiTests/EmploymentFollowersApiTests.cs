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
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B6 — CRUD-over-HTTP for the 3 nested "follower"
/// controllers (HrEmploymentControllers.cs) plus the employeeId-scoping and screen-permission checks.
/// Detailed permission-grant mechanics (screens/roles wiring) are already exhaustively covered by
/// EmployeeApiTests (Batch B5) — this file only reconfirms the 403-without-permission case per
/// controller, not the full grant flow again.
/// </summary>
public class EmploymentFollowersApiTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
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

    private async Task<long> SeedEmployeeAsync(long companyId, string code)
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

        var employee = new Employee
        {
            CompanyId = companyId, BranchId = 1, Code = code, NameAr = "موظف", NameEn = "Employee",
            OrgUnitId = orgUnit.Id, JobPositionId = jobPosition.Id, JobGradeId = jobGrade.Id,
            HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee.Id;
    }

    private HttpClient Client(long companyId, long userId = 1, string? roles = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        if (roles is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        return client;
    }

    [Fact]
    public async Task EmploymentContracts_full_crud_over_http_and_only_returns_the_route_employees_own_contracts()
    {
        var company = await SeedCompanyAsync();
        var employeeA = await SeedEmployeeAsync(company.CompanyId, "EA");
        var employeeB = await SeedEmployeeAsync(company.CompanyId, "EB");
        var admin = Client(company.CompanyId);

        var createA = await admin.PostAsJsonAsync($"/api/v1/hr/employees/{employeeA}/contracts", new
        {
            contractType = ContractType.Indefinite, startDate = "2026-01-01", endDate = (string?)null, probationEndDate = (string?)null,
            basicSalary = 5000m, insurableWage = 4500m, workingHoursPerDay = 8
        });
        createA.EnsureSuccessStatusCode();
        var contractAId = (await createA.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var createB = await admin.PostAsJsonAsync($"/api/v1/hr/employees/{employeeB}/contracts", new
        {
            contractType = ContractType.Indefinite, startDate = "2026-01-01", endDate = (string?)null, probationEndDate = (string?)null,
            basicSalary = 6000m, insurableWage = 5500m, workingHoursPerDay = 8
        });
        createB.EnsureSuccessStatusCode();

        var listA = await admin.GetAsync($"/api/v1/hr/employees/{employeeA}/contracts");
        listA.EnsureSuccessStatusCode();
        var itemsA = (await listA.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        Assert.Equal(1, itemsA.GetArrayLength());

        var getById = await admin.GetAsync($"/api/v1/hr/employees/{employeeA}/contracts/{contractAId}");
        getById.EnsureSuccessStatusCode();
        Assert.Equal(5000, (await getById.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("basicSalary").GetDecimal());

        var update = await admin.PutAsJsonAsync($"/api/v1/hr/employees/{employeeA}/contracts/{contractAId}", new
        {
            contractType = ContractType.Indefinite, startDate = "2026-01-01", endDate = (string?)null, probationEndDate = (string?)null,
            basicSalary = 5500m, insurableWage = 5000m, workingHoursPerDay = 8
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var terminate = await admin.PostAsJsonAsync($"/api/v1/hr/employees/{employeeA}/contracts/{contractAId}/terminate", new { });
        Assert.Equal(HttpStatusCode.NoContent, terminate.StatusCode);

        var delete = await admin.DeleteAsync($"/api/v1/hr/employees/{employeeA}/contracts/{contractAId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task EmployeeDocuments_and_EmployeeCertifications_full_crud_over_http()
    {
        var company = await SeedCompanyAsync();
        var employeeId = await SeedEmployeeAsync(company.CompanyId, "E1");
        var admin = Client(company.CompanyId);

        long documentTypeId, attachmentId;
        await using (var db = factory.CreateDirectDbContext(company.CompanyId))
        {
            var docType = new EmployeeDocumentType { CompanyId = company.CompanyId, Code = "ID", NameAr = "بطاقة", NameEn = "ID Card" };
            db.EmployeeDocumentTypes.Add(docType);
            var attachment = new Habbak.ERP.Domain.Common.Attachment
            {
                CompanyId = company.CompanyId, EntityType = "EmployeeDocument", EntityId = 0,
                FileName = "id.pdf", ContentType = "application/pdf", FileSizeBytes = 1, Content = [1]
            };
            db.Attachments.Add(attachment);
            await db.SaveChangesAsync();
            documentTypeId = docType.Id;
            attachmentId = attachment.Id;
        }

        var createDoc = await admin.PostAsJsonAsync($"/api/v1/hr/employees/{employeeId}/documents", new
        {
            employeeDocumentTypeId = documentTypeId, issueDate = "2026-01-01", expiryDate = (string?)null, attachmentId, documentNumber = "DOC-1"
        });
        createDoc.EnsureSuccessStatusCode();
        var docId = (await createDoc.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/hr/employees/{employeeId}/documents/{docId}")).StatusCode);

        var createCert = await admin.PostAsJsonAsync($"/api/v1/hr/employees/{employeeId}/certifications", new
        {
            nameAr = "باريستا", nameEn = "Barista", issuer = "SCA", issueDate = "2026-01-01", expiryDate = (string?)null, certificateNumber = (string?)null
        });
        createCert.EnsureSuccessStatusCode();
        var certId = (await createCert.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var listCerts = await admin.GetAsync($"/api/v1/hr/employees/{employeeId}/certifications");
        listCerts.EnsureSuccessStatusCode();
        Assert.Equal(1, (await listCerts.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/hr/employees/{employeeId}/certifications/{certId}")).StatusCode);
    }

    [Fact]
    public async Task All_three_followers_require_the_HR_EMPLOYEES_screen_permission()
    {
        var company = await SeedCompanyAsync();
        var employeeId = await SeedEmployeeAsync(company.CompanyId, "E1");
        var cashier = Client(company.CompanyId, 5, SystemRoles.Cashier);

        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync($"/api/v1/hr/employees/{employeeId}/contracts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync($"/api/v1/hr/employees/{employeeId}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync($"/api/v1/hr/employees/{employeeId}/certifications")).StatusCode);
    }
}
