using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Settings;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.Sales;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// Settings &amp; Permissions, phase 2 (Docs/Modules/Settings-Permissions-Phase2.md) over real HTTP:
/// sign-in and sessions, the screen permission filter, field masking, and the audit log.
/// </summary>
public class SecurityTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static readonly BcryptPasswordHasher Hasher = new();
    private const string Password = "Strong@123";

    private sealed record Session(
        string AccessToken, string RefreshToken, long UserId, long CompanyId, List<string> RoleCodes, bool PasswordChangeRequired);

    private sealed record Seeded(long CompanyId, Dictionary<string, long> Roles);

    // -------------------------------------------------------------------------------- arrange

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

        // The host seeds the menu at startup, before this factory has migrated its database — so it
        // is seeded here; screen permissions are validated against it.
        if (!await db.MenuItems.AnyAsync())
        {
            db.MenuItems.AddRange(Habbak.ERP.Infrastructure.Persistence.Seeding.MenuItemSeedData.Build());
            await db.SaveChangesAsync();
        }

        var company = new Company { Code = $"S{Guid.NewGuid():N}"[..12], NameAr = "شركة أمان", NameEn = "Security Co", BaseCurrencyId = currency.Id, IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        await CompanySecurityDefaults.AddAsync(db, company.Id, DateTime.UtcNow, 0, default);
        await db.SaveChangesAsync();

        var roles = await db.Roles.IgnoreQueryFilters().Where(r => r.CompanyId == company.Id).ToDictionaryAsync(r => r.Code, r => r.Id);
        return new Seeded(company.Id, roles);
    }

    private async Task<(long Id, string Username)> SeedUserAsync(Seeded company, string role, bool mustChangePassword = false)
    {
        await using var db = factory.CreateDirectDbContext(company.CompanyId);
        var username = $"u{Guid.NewGuid():N}"[..14];
        var user = new User
        {
            Username = username, Email = $"{username}@test.local", FullName = "مستخدم", PasswordHash = Hasher.Hash(Password),
            Status = UserStatus.Active, MustChangePassword = mustChangePassword, PasswordChangedAtUtc = DateTime.UtcNow
        };
        user.UserRoles.Add(new UserRole { RoleId = company.Roles[role], AssignedAtUtc = DateTime.UtcNow, AssignedByUserId = 0 });
        user.UserScopes.Add(new UserScope { CompanyId = company.CompanyId, RoleInScope = role, IsDefault = true, IsActive = true, GrantedAtUtc = DateTime.UtcNow });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, username);
    }

    private HttpClient Client(long companyId, long userId = 1, string? roles = null, bool passwordChangeRequired = false)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        if (roles is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        if (passwordChangeRequired) client.DefaultRequestHeaders.Add(TestAuthHandler.PasswordChangeHeader, "1");
        return client;
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()!;

    private Task<HttpResponseMessage> LoginAsync(string username, string password) =>
        factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { username, password });

    // ------------------------------------------------------------------------------- sign-in

    [Fact]
    public async Task Login_returns_a_signed_token_for_the_users_company_and_roles_and_is_recorded()
    {
        var company = await SeedCompanyAsync();
        var (userId, username) = await SeedUserAsync(company, SystemRoles.Cashier);

        var response = await LoginAsync(username, Password);
        response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<Session>())!;

        Assert.Equal(company.CompanyId, session.CompanyId);
        Assert.Contains(SystemRoles.Cashier, session.RoleCodes);
        Assert.False(session.PasswordChangeRequired);
        Assert.True(session.RefreshToken.Length >= 64);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        Assert.Equal(company.CompanyId.ToString(), jwt.Claims.Single(c => c.Type == AuthClaims.CompanyId).Value);
        Assert.Equal(userId.ToString(), jwt.Claims.Single(c => c.Type == AuthClaims.UserId).Value);
        Assert.Contains(jwt.Claims, c => c.Type == AuthClaims.Role && c.Value == SystemRoles.Cashier);
        Assert.InRange(jwt.ValidTo, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31)); // SessionTimeoutMinutes = 30

        await using var db = factory.CreateDirectDbContext(company.CompanyId);
        Assert.True(await db.LoginAttempts.AnyAsync(a => a.UserId == userId && a.Success));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.UserId == userId && a.ActionType == AuditActionType.Login));
        var stored = await db.RefreshTokens.SingleAsync(t => t.UserId == userId);
        Assert.NotEqual(session.RefreshToken, stored.Token); // only the hash is stored
    }

    // --------------------------------------------------------------- HR-Core-Plan.md §0.1 (EmployeeId claim)

    [Fact]
    public async Task A_user_with_no_linked_employee_gets_no_EmployeeId_claim()
    {
        var company = await SeedCompanyAsync();
        var (_, username) = await SeedUserAsync(company, SystemRoles.Cashier);

        var session = (await (await LoginAsync(username, Password)).Content.ReadFromJsonAsync<Session>())!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);

        // Phase 0: no Employee entity exists yet, so no session carries this claim at all — Phase 1
        // populates it from Employee.UserId once that link exists (Docs/Implementation/HR-Core-Plan.md §0.1).
        Assert.DoesNotContain(jwt.Claims, c => c.Type == AuthClaims.EmployeeId);
    }

    [Fact]
    public async Task A_user_with_no_linked_employee_is_unaffected_by_the_Self_data_scope()
    {
        var company = await SeedCompanyAsync();
        var (userId, username) = await SeedUserAsync(company, SystemRoles.Cashier);
        var client = Client(company.CompanyId, userId, SystemRoles.Cashier);

        // No EmployeeId claim on this session means CurrentEmployeeId is null in AppDbContext, which
        // the IEmployeeScopedEntity filter treats as "no restriction" — proven generically against a
        // throwaway entity in Habbak.ERP.IntegrationTests.EmployeeScopedEntityTests. Here we only
        // confirm an ordinary, unrelated request still succeeds normally for such a user.
        var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_user_get_the_same_answer_and_both_are_recorded()
    {
        var company = await SeedCompanyAsync();
        var (userId, username) = await SeedUserAsync(company, SystemRoles.Cashier);

        var wrong = await LoginAsync(username, "Wrong@123");
        var unknown = await LoginAsync($"ghost{Guid.NewGuid():N}"[..14], Password);

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await ErrorCodeAsync(wrong), await ErrorCodeAsync(unknown));

        await using var db = factory.CreateDirectDbContext(company.CompanyId);
        Assert.Equal("WrongPassword", (await db.LoginAttempts.SingleAsync(a => a.UserId == userId)).FailureReason);
        Assert.Equal(1, (await db.Users.SingleAsync(u => u.Id == userId)).FailedLoginAttempts);
    }

    [Fact]
    public async Task Fifth_wrong_password_locks_the_account_even_against_the_right_password()
    {
        var company = await SeedCompanyAsync();
        var (userId, username) = await SeedUserAsync(company, SystemRoles.Cashier);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal("AUTH-INVALID-CREDENTIALS", await ErrorCodeAsync(await LoginAsync(username, "Wrong@123")));
        }

        Assert.Equal("AUTH-USER-LOCKED", await ErrorCodeAsync(await LoginAsync(username, "Wrong@123")));
        Assert.Equal("AUTH-USER-LOCKED", await ErrorCodeAsync(await LoginAsync(username, Password)));

        // An administrator unlocks it; the right password works again.
        var unlock = await Client(company.CompanyId).PostAsync($"/api/v1/settings/users/{userId}/unlock", null);
        Assert.Equal(HttpStatusCode.NoContent, unlock.StatusCode);
        (await LoginAsync(username, Password)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_a_reused_token_ends_every_session()
    {
        var company = await SeedCompanyAsync();
        var (userId, username) = await SeedUserAsync(company, SystemRoles.Cashier);
        var first = (await (await LoginAsync(username, Password)).Content.ReadFromJsonAsync<Session>())!;
        var client = factory.CreateClient();

        var rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        rotated.EnsureSuccessStatusCode();
        var second = (await rotated.Content.ReadFromJsonAsync<Session>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        // The old token again: stolen copy — refused, and the legitimate new one dies with it.
        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        var afterReuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);

        await using var db = factory.CreateDirectDbContext(company.CompanyId);
        Assert.All(await db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync(), t => Assert.NotNull(t.RevokedAtUtc));
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var company = await SeedCompanyAsync();
        var (_, username) = await SeedUserAsync(company, SystemRoles.Cashier);
        var session = (await (await LoginAsync(username, Password)).Content.ReadFromJsonAsync<Session>())!;
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = session.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task A_user_who_must_change_the_password_can_do_nothing_else_until_they_do()
    {
        var company = await SeedCompanyAsync();
        var (userId, username) = await SeedUserAsync(company, SystemRoles.CompanyAdmin, mustChangePassword: true);

        var session = (await (await LoginAsync(username, Password)).Content.ReadFromJsonAsync<Session>())!;
        Assert.True(session.PasswordChangeRequired);
        Assert.Contains(new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken).Claims, c => c.Type == AuthClaims.PasswordChangeRequired);

        var client = Client(company.CompanyId, userId, SystemRoles.CompanyAdmin, passwordChangeRequired: true);
        var blocked = await client.GetAsync("/api/v1/accounting/journal-entries");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("AUTH-PASSWORD-CHANGE-REQUIRED", await ErrorCodeAsync(blocked));

        var weak = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = Password, newPassword = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var changed = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = Password, newPassword = "Better#2026" });
        changed.EnsureSuccessStatusCode();
        Assert.False((await changed.Content.ReadFromJsonAsync<Session>())!.PasswordChangeRequired);

        (await LoginAsync(username, "Better#2026")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(username, Password)).StatusCode);
    }

    // --------------------------------------------------------------------- screen permissions

    [Fact]
    public async Task Screens_need_a_permission_but_reference_data_is_readable_by_anyone_signed_in()
    {
        var company = await SeedCompanyAsync();
        var accountant = Client(company.CompanyId, 5, SystemRoles.Accountant);

        var denied = await accountant.GetAsync("/api/v1/accounting/journal-entries");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("PERMISSION-DENIED", await ErrorCodeAsync(denied));
        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync("/api/v1/inventory/items")).StatusCode); // lookup read

        var grant = await Client(company.CompanyId).PutAsJsonAsync(
            $"/api/v1/settings/roles/{company.Roles[SystemRoles.Accountant]}/screens",
            new[] { new { screenCode = "ACCOUNTING_JOURNAL_ENTRIES", canView = true, canAdd = false, canEdit = false, canDelete = false, canPrint = true, canExport = false, canApprove = false } });
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync("/api/v1/accounting/journal-entries")).StatusCode);
        var add = await accountant.PostAsJsonAsync("/api/v1/accounting/journal-entries", new { });
        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PostAsJsonAsync("/api/v1/inventory/items", new { })).StatusCode); // lookup, but writing
    }

    [Fact]
    public async Task Posting_a_document_needs_approve_not_just_edit()
    {
        var company = await SeedCompanyAsync();
        var admin = Client(company.CompanyId);
        var officer = Client(company.CompanyId, 6, SystemRoles.PurchasingOfficer);
        var roleId = company.Roles[SystemRoles.PurchasingOfficer];

        async Task GrantAsync(bool approve) =>
            (await admin.PutAsJsonAsync($"/api/v1/settings/roles/{roleId}/screens", new[]
            {
                new { screenCode = "PURCHASING_PURCHASE_INVOICES", canView = true, canAdd = true, canEdit = true, canDelete = false, canPrint = false, canExport = false, canApprove = approve }
            })).EnsureSuccessStatusCode();

        await GrantAsync(approve: false);
        var refused = await officer.PostAsync("/api/v1/purchasing/purchase-invoices/999999999/post", null);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        await GrantAsync(approve: true);
        var allowed = await officer.PostAsync("/api/v1/purchasing/purchase-invoices/999999999/post", null);
        Assert.Equal(HttpStatusCode.NotFound, allowed.StatusCode); // past the permission check
    }

    [Fact]
    public async Task The_menu_shows_only_the_screens_the_user_may_view()
    {
        var company = await SeedCompanyAsync();
        (await Client(company.CompanyId).PutAsJsonAsync($"/api/v1/settings/roles/{company.Roles[SystemRoles.StoreKeeper]}/screens", new[]
        {
            new { screenCode = "INVENTORY_STOCK_IN", canView = true, canAdd = true, canEdit = false, canDelete = false, canPrint = false, canExport = false, canApprove = false }
        })).EnsureSuccessStatusCode();

        var menu = await Client(company.CompanyId, 7, SystemRoles.StoreKeeper).GetFromJsonAsync<JsonElement>("/api/v1/navigation/menu");
        var group = Assert.Single(menu.EnumerateArray());
        var leaf = Assert.Single(group.GetProperty("children").EnumerateArray());
        Assert.Equal("INVENTORY_STOCK_IN", leaf.GetProperty("code").GetString());

        var full = await Client(company.CompanyId).GetFromJsonAsync<JsonElement>("/api/v1/navigation/menu");
        Assert.True(full.GetArrayLength() > 3);
    }

    [Fact]
    public void Every_controller_says_which_screen_it_belongs_to()
    {
        var unmapped = typeof(ScreenAttribute).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttribute<ScreenAttribute>(inherit: true) is null && t.GetCustomAttribute<AnySignedInUserAttribute>() is null)
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(unmapped);
    }

    // ------------------------------------------------------------------- fields and the audit log

    [Fact]
    public async Task A_hidden_field_comes_back_empty_and_is_left_alone_on_save()
    {
        var company = await SeedCompanyAsync();
        long customerId;
        await using (var db = factory.CreateDirectDbContext(company.CompanyId))
        {
            var customer = new Customer
            {
                CompanyId = company.CompanyId, Code = $"C{Guid.NewGuid():N}"[..10], NameAr = "عميل", NameEn = "Customer",
                CustomerType = CustomerType.Individual, CreditLimit = 5000, Phone = "0100", IsActive = true
            };
            db.Customers.Add(customer);
            db.FieldPermissions.Add(new FieldPermission
            {
                CompanyId = company.CompanyId, RoleId = company.Roles[SystemRoles.SalesRep], ScreenCode = "SALES_CUSTOMERS", EntityType = "Customer", FieldName = "CreditLimit",
                CanView = false, CanEdit = false, RequiresAuditLog = true
            });
            await db.SaveChangesAsync();
            customerId = customer.Id;
        }

        var (repId, _) = await SeedUserAsync(company, SystemRoles.SalesRep);
        var rep = Client(company.CompanyId, repId, SystemRoles.SalesRep);
        var seen = await rep.GetFromJsonAsync<JsonElement>($"/api/v1/sales/customers/{customerId}");
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("creditLimit").ValueKind);
        Assert.Equal("0100", seen.GetProperty("phone").GetString());

        var admin = Client(company.CompanyId);
        Assert.Equal(5000m, (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/sales/customers/{customerId}")).GetProperty("creditLimit").GetDecimal());

        // The rep may edit customers but not the credit limit: their save must not wipe it.
        (await admin.PutAsJsonAsync($"/api/v1/settings/roles/{company.Roles[SystemRoles.SalesRep]}/screens", new[]
        {
            new { screenCode = "SALES_CUSTOMERS", canView = true, canAdd = false, canEdit = true, canDelete = false, canPrint = false, canExport = false, canApprove = false }
        })).EnsureSuccessStatusCode();
        var update = await rep.PutAsJsonAsync($"/api/v1/sales/customers/{customerId}", new
        {
            branchId = (long?)null, nameAr = "عميل معدّل", nameEn = "Customer", customerType = "Individual", phone = "0100", email = (string?)null,
            address = (string?)null, creditLimit = 0m, paymentTermDays = 0, receivableAccountId = (long?)null, loyaltyTierId = (long?)null, isActive = true
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        await using var check = factory.CreateDirectDbContext(company.CompanyId);
        var stored = await check.Customers.SingleAsync(c => c.Id == customerId);
        Assert.Equal(5000m, stored.CreditLimit);
        Assert.Equal("عميل معدّل", stored.NameAr);
    }

    [Fact]
    public async Task Changes_land_in_the_audit_log_with_sensitive_values_redacted()
    {
        var company = await SeedCompanyAsync();
        var (adminId, _) = await SeedUserAsync(company, SystemRoles.CompanyAdmin);
        var admin = Client(company.CompanyId, adminId);

        var created = await admin.PostAsJsonAsync("/api/v1/sales/customers", new
        {
            code = $"A{Guid.NewGuid():N}"[..10], branchId = (long?)null, nameAr = "عميل للمراجعة", nameEn = "Audited", customerType = "Individual",
            phone = "0111", email = (string?)null, address = (string?)null, creditLimit = 1000m, paymentTermDays = 0,
            receivableAccountId = (long?)null, loyaltyTierId = (long?)null
        });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        (await admin.PutAsJsonAsync($"/api/v1/sales/customers/{id}", new
        {
            branchId = (long?)null, nameAr = "عميل بعد التعديل", nameEn = "Audited", customerType = "Individual", phone = "0111", email = (string?)null,
            address = (string?)null, creditLimit = 2500m, paymentTermDays = 0, receivableAccountId = (long?)null, loyaltyTierId = (long?)null, isActive = true
        })).EnsureSuccessStatusCode();

        var log = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/settings/audit-log?entityType=Customer&entityId={id}");
        var rows = log.GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(rows, r => r.GetProperty("actionType").GetString() == "Create" && r.GetProperty("userId").GetInt64() == adminId);
        var name = rows.Single(r => r.GetProperty("fieldName").GetString() == "NameAr");
        Assert.Equal("عميل للمراجعة", name.GetProperty("oldValue").GetString());
        Assert.Equal("عميل بعد التعديل", name.GetProperty("newValue").GetString());
        var credit = rows.Single(r => r.GetProperty("fieldName").GetString() == "CreditLimit");
        Assert.Equal(AuditLog.Redacted, credit.GetProperty("oldValue").GetString());
        Assert.Equal(AuditLog.Redacted, credit.GetProperty("newValue").GetString());
    }

    // -------------------------------------------------------------------------- administration

    [Fact]
    public async Task Admin_creates_a_user_who_can_then_sign_in_and_a_weak_password_is_refused()
    {
        var company = await SeedCompanyAsync();
        var admin = Client(company.CompanyId);
        var username = $"new{Guid.NewGuid():N}"[..12];

        var weak = await admin.PostAsJsonAsync("/api/v1/settings/users", new
        {
            username, email = $"{username}@test.local", fullName = "مستخدم جديد", phoneNumber = (string?)null, preferredLanguage = "Arabic",
            password = "weakpass", mustChangePassword = false, roleIds = new[] { company.Roles[SystemRoles.Cashier] }
        });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var created = await admin.PostAsJsonAsync("/api/v1/settings/users", new
        {
            username, email = $"{username}@test.local", fullName = "مستخدم جديد", phoneNumber = (string?)null, preferredLanguage = "Arabic",
            password = Password, mustChangePassword = true, roleIds = new[] { company.Roles[SystemRoles.Cashier] }
        });
        created.EnsureSuccessStatusCode();

        var session = (await (await LoginAsync(username, Password)).Content.ReadFromJsonAsync<Session>())!;
        Assert.Equal(company.CompanyId, session.CompanyId);
        Assert.Equal([SystemRoles.Cashier], session.RoleCodes);
        Assert.True(session.PasswordChangeRequired);

        // Only a super admin hands out SUPER_ADMIN.
        var companyAdmin = Client(company.CompanyId, 9, SystemRoles.CompanyAdmin);
        var escalate = await companyAdmin.PutAsJsonAsync($"/api/v1/settings/users/{session.UserId}/roles", new[] { new { roleId = company.Roles[SystemRoles.SuperAdmin], branchId = (long?)null, expiresAtUtc = (DateTime?)null } });
        Assert.Equal(HttpStatusCode.Forbidden, escalate.StatusCode);
    }

    // ------------------------------------------------------------------------------ branch scope

    [Fact]
    public async Task A_session_limited_to_a_branch_sees_and_records_only_that_branch()
    {
        var company = await SeedCompanyAsync();
        long branchA, branchB;
        await using (var db = factory.CreateDirectDbContext(company.CompanyId))
        {
            var a = new Branch { CompanyId = company.CompanyId, Code = $"A{Guid.NewGuid():N}"[..8], NameAr = "فرع المعادي", NameEn = "Maadi" };
            var b = new Branch { CompanyId = company.CompanyId, Code = $"B{Guid.NewGuid():N}"[..8], NameAr = "فرع طنطا", NameEn = "Tanta" };
            db.Branches.AddRange(a, b);
            await db.SaveChangesAsync();
            (branchA, branchB) = (a.Id, b.Id);
        }

        // Signing in through a branch scope puts the branch in the session.
        var (userId, username) = await SeedUserAsync(company, SystemRoles.CompanyAdmin);
        await using (var db = factory.CreateDirectDbContext(company.CompanyId))
        {
            var scope = await db.UserScopes.SingleAsync(s => s.UserId == userId);
            scope.BranchId = branchA;
            await db.SaveChangesAsync();
        }

        var session = await (await LoginAsync(username, Password)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(branchA, session.GetProperty("branchId").GetInt64());
        Assert.Equal("فرع المعادي", session.GetProperty("scopes")[0].GetProperty("branchNameAr").GetString());
        Assert.Contains(new JwtSecurityTokenHandler().ReadJwtToken(session.GetProperty("accessToken").GetString()).Claims,
            c => c.Type == AuthClaims.BranchId && c.Value == branchA.ToString());

        var client = Client(company.CompanyId, userId, SystemRoles.CompanyAdmin);
        client.DefaultRequestHeaders.Add(TestAuthHandler.BranchIdHeader, branchA.ToString());

        var branches = await client.GetFromJsonAsync<JsonElement>("/api/v1/organization/branches");
        Assert.Equal([branchA], branches.EnumerateArray().Select(b => b.GetProperty("id").GetInt64()).ToList());

        var elsewhere = await client.PostAsJsonAsync("/api/v1/inventory/custody-officers",
            new { code = $"CB{Guid.NewGuid():N}"[..10], nameAr = "مسؤول", nameEn = "Officer", branchId = branchB });
        Assert.Equal(HttpStatusCode.Forbidden, elsewhere.StatusCode);
        Assert.Equal("BRANCH-OUT-OF-SCOPE", await ErrorCodeAsync(elsewhere));

        var own = await client.PostAsJsonAsync("/api/v1/inventory/custody-officers",
            new { code = $"CA{Guid.NewGuid():N}"[..10], nameAr = "مسؤول", nameEn = "Officer", branchId = branchA });
        own.EnsureSuccessStatusCode();
    }
}
