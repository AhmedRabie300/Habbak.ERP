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
/// Docs/Implementation/Phase-3B-Research.md §Phase 3B — CRUD-over-HTTP + screen-permission
/// enforcement على AttendanceDevicesController (نفس نمط HrLookupsApiTests)، وإثبات إن الـPush
/// endpoint فعلًا Anonymous (بدون أي JWT) لكنه بيتحقق من سر الجهاز بنفسه.
/// </summary>
public class AttendanceDevicesApiTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
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
    public async Task Attendance_devices_full_crud_over_http_and_secret_is_only_returned_on_create_and_regenerate()
    {
        var company = await SeedCompanyAsync();
        var admin = Client(company.CompanyId);
        var serial = $"SN{Guid.NewGuid():N}"[..16];

        var create = await admin.PostAsJsonAsync("/api/v1/hr/attendance-devices", new { code = $"D{Guid.NewGuid():N}"[..8], nameAr = "جهاز", nameEn = "Device", model = "ZKTeco K40", serialNumber = serial });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt64();
        var firstSecret = created.GetProperty("deviceSecret").GetString();
        Assert.False(string.IsNullOrWhiteSpace(firstSecret));

        var getById = await admin.GetAsync($"/api/v1/hr/attendance-devices/{id}");
        getById.EnsureSuccessStatusCode();
        var fetched = await getById.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(fetched.TryGetProperty("deviceSecret", out _)); // مفيش Hash ولا سر في القراءة العادية.
        Assert.Equal(serial, fetched.GetProperty("serialNumber").GetString());

        var update = await admin.PutAsJsonAsync($"/api/v1/hr/attendance-devices/{id}", new { nameAr = "جهاز٢", nameEn = "Device II", model = "ZKTeco K40", serialNumber = serial, isActive = true });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var regenerate = await admin.PostAsync($"/api/v1/hr/attendance-devices/{id}/regenerate-secret", null);
        regenerate.EnsureSuccessStatusCode();
        var secondSecret = (await regenerate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceSecret").GetString();
        Assert.NotEqual(firstSecret, secondSecret);

        var delete = await admin.DeleteAsync($"/api/v1/hr/attendance-devices/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/hr/attendance-devices/{id}")).StatusCode);
    }

    [Fact]
    public async Task Attendance_devices_need_the_screen_permission_to_create()
    {
        var company = await SeedCompanyAsync();
        var cashierUserId = await SeedUserAsync(company, SystemRoles.Cashier);
        var cashier = Client(company.CompanyId, cashierUserId, SystemRoles.Cashier);

        var denied = await cashier.PostAsJsonAsync("/api/v1/hr/attendance-devices", new { code = $"D{Guid.NewGuid():N}"[..8], nameAr = "س", nameEn = "X" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("PERMISSION-DENIED", await ErrorCodeAsync(denied));

        var grant = await Client(company.CompanyId).PutAsJsonAsync(
            $"/api/v1/settings/roles/{company.Roles[SystemRoles.Cashier]}/screens",
            new[] { new { screenCode = "HR_ATTENDANCE_DEVICES", canView = true, canAdd = true, canEdit = false, canDelete = false, canPrint = false, canExport = false, canApprove = false } });
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);

        var allowed = await cashier.PostAsJsonAsync("/api/v1/hr/attendance-devices", new { code = $"D{Guid.NewGuid():N}"[..8], nameAr = "س", nameEn = "X" });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    // --------------------------------------------------------------------- Push (anonymous, device-secret auth)

    [Fact]
    public async Task Push_json_accepts_a_correct_secret_and_ingests_punches_with_no_jwt_at_all()
    {
        var company = await SeedCompanyAsync();
        var admin = Client(company.CompanyId);
        var serial = $"SN{Guid.NewGuid():N}"[..16];

        var create = await admin.PostAsJsonAsync("/api/v1/hr/attendance-devices", new { code = $"D{Guid.NewGuid():N}"[..8], nameAr = "جهاز", nameEn = "Device", serialNumber = serial });
        create.EnsureSuccessStatusCode();
        var secret = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceSecret").GetString();

        var anonymous = factory.CreateClient(); // بدون أي Header مصادقة (Company/User/Roles) — نفس جهاز فعلي.
        anonymous.DefaultRequestHeaders.Add("X-Device-Secret", secret);
        var push = await anonymous.PostAsJsonAsync("/api/v1/hr/attendance-devices/push-json", new
        {
            serialNumber = serial,
            punches = new[] { new { deviceUserId = "77", timestampUtc = DateTime.UtcNow, status = 0, verifyType = 1 } }
        });

        push.EnsureSuccessStatusCode();
        var result = await push.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, result.GetProperty("accepted").GetInt32());
    }

    [Fact]
    public async Task Push_json_rejects_a_wrong_secret()
    {
        var company = await SeedCompanyAsync();
        var admin = Client(company.CompanyId);
        var serial = $"SN{Guid.NewGuid():N}"[..16];
        (await admin.PostAsJsonAsync("/api/v1/hr/attendance-devices", new { code = $"D{Guid.NewGuid():N}"[..8], nameAr = "جهاز", nameEn = "Device", serialNumber = serial })).EnsureSuccessStatusCode();

        var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Device-Secret", "not-the-real-secret");
        var push = await anonymous.PostAsJsonAsync("/api/v1/hr/attendance-devices/push-json", new { serialNumber = serial, punches = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.Unauthorized, push.StatusCode);
    }
}
