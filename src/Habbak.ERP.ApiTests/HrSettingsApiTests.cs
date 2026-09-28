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
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.0 — HrSettingsController
/// (/api/v1/hr/settings). Presence of [Screen("HR_SETTINGS")] is already covered by the reflection
/// test SecurityTests.Every_controller_says_which_screen_it_belongs_to; this file exercises the
/// actual HTTP round trip and the screen-permission gate.
/// </summary>
public class HrSettingsApiTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
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

    [Fact]
    public async Task Get_returns_defaults_then_update_persists_and_is_read_back()
    {
        var company = await SeedCompanyAsync();
        var admin = Client(company.CompanyId);

        var initial = await admin.GetAsync("/api/v1/hr/settings");
        initial.EnsureSuccessStatusCode();
        var initialBody = await initial.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(90, initialBody.GetProperty("defaultProbationDays").GetInt32());
        Assert.True(initialBody.GetProperty("requireNationalIdForActivation").GetBoolean());

        var update = await admin.PutAsJsonAsync("/api/v1/hr/settings", new { defaultProbationDays = 45, defaultBranchId = (long?)null, requireNationalIdForActivation = false });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var after = await admin.GetAsync("/api/v1/hr/settings");
        after.EnsureSuccessStatusCode();
        var afterBody = await after.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(45, afterBody.GetProperty("defaultProbationDays").GetInt32());
        Assert.False(afterBody.GetProperty("requireNationalIdForActivation").GetBoolean());
    }

    [Fact]
    public async Task Writing_needs_the_screen_permission_but_reading_does_not_require_the_add_right()
    {
        var company = await SeedCompanyAsync();
        var cashierUserId = await SeedUserAsync(company, SystemRoles.Cashier);
        var cashier = Client(company.CompanyId, cashierUserId, SystemRoles.Cashier);

        var deniedRead = await cashier.GetAsync("/api/v1/hr/settings");
        Assert.Equal(HttpStatusCode.Forbidden, deniedRead.StatusCode);

        var deniedWrite = await cashier.PutAsJsonAsync("/api/v1/hr/settings", new { defaultProbationDays = 30, defaultBranchId = (long?)null, requireNationalIdForActivation = true });
        Assert.Equal(HttpStatusCode.Forbidden, deniedWrite.StatusCode);
        Assert.Equal("PERMISSION-DENIED", await ErrorCodeAsync(deniedWrite));

        var grant = await Client(company.CompanyId).PutAsJsonAsync(
            $"/api/v1/settings/roles/{company.Roles[SystemRoles.Cashier]}/screens",
            new[] { new { screenCode = "HR_SETTINGS", canView = true, canAdd = true, canEdit = true, canDelete = false, canPrint = false, canExport = false, canApprove = false } });
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);

        var allowedWrite = await cashier.PutAsJsonAsync("/api/v1/hr/settings", new { defaultProbationDays = 30, defaultBranchId = (long?)null, requireNationalIdForActivation = true });
        Assert.Equal(HttpStatusCode.NoContent, allowedWrite.StatusCode);
    }
}
