using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habbak.ERP.API.Auth;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B4 — CRUD-over-HTTP + screen-permission
/// enforcement for the 8 lookup controllers (HrLookupControllers.cs). Two representative
/// controllers are exercised in full: CountriesController (system-wide, Organization module,
/// /api/v1/organization/countries) and OrgUnitsController (company-scoped, HR module, hierarchical,
/// /api/v1/hr/org-units) — the other 6 share the exact same handler/controller shape (confirmed by
/// the Application-layer HrLookupsBatch4Tests, Batch B4) and are all covered by the existing
/// reflection test SecurityTests.Every_controller_says_which_screen_it_belongs_to, which fails the
/// build if any of the 8 is missing its [Screen] attribute.
/// </summary>
public class HrLookupsApiTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
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

    // --------------------------------------------------------------------- Countries (system-wide)

    [Fact]
    public async Task Countries_full_crud_over_http()
    {
        var company = await SeedCompanyAsync();
        var admin = Client(company.CompanyId);

        var create = await admin.PostAsJsonAsync("/api/v1/organization/countries", new { code = $"C{Guid.NewGuid():N}"[..8], nameAr = "مصر", nameEn = "Egypt", isoCode = "EG" });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var getById = await admin.GetAsync($"/api/v1/organization/countries/{id}");
        getById.EnsureSuccessStatusCode();
        Assert.Equal("Egypt", (await getById.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("nameEn").GetString());

        var list = await admin.GetAsync("/api/v1/organization/countries");
        list.EnsureSuccessStatusCode();

        var update = await admin.PutAsJsonAsync($"/api/v1/organization/countries/{id}", new { nameAr = "مصر٢", nameEn = "Egypt II", isoCode = "EG", isActive = true });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var delete = await admin.DeleteAsync($"/api/v1/organization/countries/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/organization/countries/{id}")).StatusCode);
    }

    [Fact]
    public async Task Countries_are_readable_by_anyone_signed_in_but_writing_needs_the_screens_permission()
    {
        var company = await SeedCompanyAsync();
        var cashierUserId = await SeedUserAsync(company, SystemRoles.Cashier);
        var cashier = Client(company.CompanyId, cashierUserId, SystemRoles.Cashier);

        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync("/api/v1/organization/countries")).StatusCode); // LookupReads = true

        var denied = await cashier.PostAsJsonAsync("/api/v1/organization/countries", new { code = "XX", nameAr = "س", nameEn = "X" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("PERMISSION-DENIED", await ErrorCodeAsync(denied));

        var grant = await Client(company.CompanyId).PutAsJsonAsync(
            $"/api/v1/settings/roles/{company.Roles[SystemRoles.Cashier]}/screens",
            new[] { new { screenCode = "SETTINGS_COUNTRIES", canView = true, canAdd = true, canEdit = false, canDelete = false, canPrint = false, canExport = false, canApprove = false } });
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);

        var allowed = await cashier.PostAsJsonAsync("/api/v1/organization/countries", new { code = $"C{Guid.NewGuid():N}"[..8], nameAr = "س", nameEn = "X" });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    // --------------------------------------------------------------------- OrgUnits (company-scoped, hierarchical)

    [Fact]
    public async Task OrgUnits_full_crud_over_http_including_cycle_and_children_rules()
    {
        var company = await SeedCompanyAsync();
        var admin = Client(company.CompanyId);

        var createRoot = await admin.PostAsJsonAsync("/api/v1/hr/org-units", new { code = "ROOT", nameAr = "الجذر", nameEn = "Root" });
        createRoot.EnsureSuccessStatusCode();
        var rootId = (await createRoot.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var createChild = await admin.PostAsJsonAsync("/api/v1/hr/org-units", new { code = "CHILD", nameAr = "فرعي", nameEn = "Child", parentId = rootId });
        createChild.EnsureSuccessStatusCode();
        var childId = (await createChild.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        // Cycle detection: reassigning Root's parent to its own child is rejected (409, BusinessRuleException).
        var cyclic = await admin.PutAsJsonAsync($"/api/v1/hr/org-units/{rootId}", new { nameAr = "الجذر", nameEn = "Root", parentId = childId, isActive = true });
        Assert.Equal(HttpStatusCode.Conflict, cyclic.StatusCode);
        Assert.Equal("HR-ORG-UNIT-PARENT-CYCLE", await ErrorCodeAsync(cyclic));

        // Delete blocked while it still has a child.
        var blockedDelete = await admin.DeleteAsync($"/api/v1/hr/org-units/{rootId}");
        Assert.Equal(HttpStatusCode.Conflict, blockedDelete.StatusCode);
        Assert.Equal("HR-ORG-UNIT-HAS-CHILDREN", await ErrorCodeAsync(blockedDelete));

        // Delete the leaf, then the now-childless root — both succeed.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/hr/org-units/{childId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/hr/org-units/{rootId}")).StatusCode);
    }

    [Fact]
    public async Task OrgUnits_need_a_permission_to_write_even_though_anyone_signed_in_can_read_them()
    {
        var company = await SeedCompanyAsync();
        var cashier = Client(company.CompanyId, 5, SystemRoles.Cashier);

        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync("/api/v1/hr/org-units")).StatusCode);

        var denied = await cashier.PostAsJsonAsync("/api/v1/hr/org-units", new { code = "X", nameAr = "س", nameEn = "X" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("PERMISSION-DENIED", await ErrorCodeAsync(denied));
    }
}
