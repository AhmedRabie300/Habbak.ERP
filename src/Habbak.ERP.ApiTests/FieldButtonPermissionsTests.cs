using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Habbak.ERP.API.Auth;
using Habbak.ERP.ApiTests.OperationalCycle;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.Sales;
using Habbak.ERP.Domain.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// Field and button permissions per screen (Docs/Modules/Field-Button-Permissions.md): a field rule
/// holds on one screen only, the screen being the one the request comes from (X-Screen-Code); a
/// special button follows its ButtonPermission row, else the screen permission it falls back on.
/// </summary>
public class FieldButtonPermissionsTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private sealed record Rights(string Screen, bool View = true, bool Add = false, bool Edit = false, bool Delete = false, bool Approve = false);

    // -------------------------------------------------------------------------------- arrange

    private async Task<long> SeedCompanyAsync()
    {
        await using var db = factory.CreateDirectDbContext(0);
        var currency = await db.Currencies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Code == "EGP");
        if (currency is null)
        {
            currency = new Currency { Code = "EGP", NameAr = "جنيه مصري", NameEn = "Egyptian Pound", IsActive = true };
            db.Currencies.Add(currency);
            await db.SaveChangesAsync();
        }

        var company = new Company { Code = $"F{Guid.NewGuid():N}"[..12], NameAr = "شركة", NameEn = "Co", BaseCurrencyId = currency.Id, IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    /// <summary>A custom role with the given screen permissions — seeded before any request, so no cache is stale.</summary>
    private async Task<(long Id, string Code)> SeedRoleAsync(long companyId, params Rights[] screens)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        var role = new Role { CompanyId = companyId, Code = $"R{Guid.NewGuid():N}"[..12].ToUpperInvariant(), NameAr = "دور", NameEn = "Role", IsActive = true };
        foreach (var s in screens)
        {
            role.ScreenPermissions.Add(new ScreenPermission
            {
                CompanyId = companyId, ScreenCode = s.Screen, CanView = s.View, CanAdd = s.Add, CanEdit = s.Edit, CanDelete = s.Delete, CanApprove = s.Approve
            });
        }

        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return (role.Id, role.Code);
    }

    private HttpClient Client(long companyId, string? roles = null, string? screen = null, long userId = 1)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        if (roles is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        if (screen is not null) client.DefaultRequestHeaders.Add(HttpCurrentScreen.HeaderName, screen);
        return client;
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()!;

    private static Task<HttpResponseMessage> SetButtonsAsync(HttpClient admin, long roleId, string screen, params object[] buttons) =>
        admin.PutAsJsonAsync($"/api/v1/settings/roles/{roleId}/buttons?screenCode={screen}", buttons);

    private async Task<long> SeedCustomerAsync(long companyId)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        var customer = new Customer
        {
            CompanyId = companyId, Code = $"C{Guid.NewGuid():N}"[..10], NameAr = "عميل", NameEn = "Customer",
            CustomerType = CustomerType.Individual, CreditLimit = 5000, Phone = "0100", IsActive = true
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    // ------------------------------------------------------------------------------- fields

    [Fact]
    public async Task The_same_field_follows_the_rule_of_the_screen_the_request_comes_from()
    {
        var companyId = await SeedCompanyAsync();
        var customerId = await SeedCustomerAsync(companyId);
        var role = await SeedRoleAsync(companyId, new Rights("SALES_CUSTOMERS"), new Rights("SALES_INVOICES"));
        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            // The phone is hidden on the invoice screen only.
            db.FieldPermissions.Add(new FieldPermission
            {
                CompanyId = companyId, RoleId = role.Id, ScreenCode = "SALES_INVOICES", EntityType = "Customer", FieldName = "Phone",
                CanView = false, CanEdit = false, RequiresAuditLog = true
            });
            await db.SaveChangesAsync();
        }

        var onCustomers = await Client(companyId, role.Code, "SALES_CUSTOMERS").GetFromJsonAsync<JsonElement>($"/api/v1/sales/customers/{customerId}");
        var onInvoices = await Client(companyId, role.Code, "SALES_INVOICES").GetFromJsonAsync<JsonElement>($"/api/v1/sales/customers/{customerId}");
        var noHeader = await Client(companyId, role.Code).GetFromJsonAsync<JsonElement>($"/api/v1/sales/customers/{customerId}");

        Assert.Equal("0100", onCustomers.GetProperty("phone").GetString());
        Assert.Equal(JsonValueKind.Null, onInvoices.GetProperty("phone").ValueKind);
        Assert.Equal("0100", noHeader.GetProperty("phone").GetString());   // the controller's own screen: SALES_CUSTOMERS
        Assert.Equal(5000m, onInvoices.GetProperty("creditLimit").GetDecimal());
    }

    [Fact]
    public async Task A_screen_header_the_user_cannot_view_is_ignored()
    {
        var companyId = await SeedCompanyAsync();
        var customerId = await SeedCustomerAsync(companyId);
        var role = await SeedRoleAsync(companyId, new Rights("SALES_CUSTOMERS"));
        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            db.FieldPermissions.Add(new FieldPermission
            {
                CompanyId = companyId, RoleId = role.Id, ScreenCode = "SALES_CUSTOMERS", EntityType = "Customer", FieldName = "CreditLimit",
                CanView = false, CanEdit = false, RequiresAuditLog = true
            });
            await db.SaveChangesAsync();
        }

        // Claiming a screen without its rule does not escape the rule: SALES_INVOICES is not viewable
        // for this role, so the request stays on SALES_CUSTOMERS where the limit is hidden.
        var seen = await Client(companyId, role.Code, "SALES_INVOICES").GetFromJsonAsync<JsonElement>($"/api/v1/sales/customers/{customerId}");

        Assert.Equal(JsonValueKind.Null, seen.GetProperty("creditLimit").ValueKind);
    }

    [Fact]
    public async Task A_field_rule_can_only_be_set_on_a_screen_that_lists_the_field()
    {
        var companyId = await SeedCompanyAsync();
        var role = await SeedRoleAsync(companyId);
        var admin = Client(companyId);

        var wrongScreen = await admin.PutAsJsonAsync($"/api/v1/settings/roles/{role.Id}/fields?screenCode=POS_SHIFTS", new[]
        {
            new { screenCode = "POS_SHIFTS", entityType = "Customer", fieldName = "Phone", canView = false, canEdit = false, requiresAuditLog = true }
        });
        var right = await admin.PutAsJsonAsync($"/api/v1/settings/roles/{role.Id}/fields?screenCode=SALES_INVOICES", new[]
        {
            new { screenCode = "SALES_INVOICES", entityType = "SalesInvoice", fieldName = "DiscountAmount", canView = true, canEdit = false, requiresAuditLog = true }
        });

        Assert.Equal("SET-FIELD-UNKNOWN", await ErrorCodeAsync(wrongScreen));
        Assert.Equal(HttpStatusCode.NoContent, right.StatusCode);

        var fields = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/permissions/role/{role.Id}/fields?screenCode=SALES_INVOICES");
        var discount = fields.EnumerateArray().Single(f => f.GetProperty("fieldName").GetString() == "DiscountAmount");
        var phone = fields.EnumerateArray().Single(f => f.GetProperty("entityType").GetString() == "Customer" && f.GetProperty("fieldName").GetString() == "Phone");
        Assert.True(discount.GetProperty("isConfigured").GetBoolean());
        Assert.False(discount.GetProperty("canEdit").GetBoolean());
        Assert.False(phone.GetProperty("isConfigured").GetBoolean());
        Assert.True(phone.GetProperty("canView").GetBoolean());
    }

    [Fact]
    public async Task Field_rows_are_unique_per_role_screen_and_field_but_the_same_field_may_differ_by_screen()
    {
        var companyId = await SeedCompanyAsync();
        var role = await SeedRoleAsync(companyId);
        FieldPermission Row(string screen) => new()
        {
            CompanyId = companyId, RoleId = role.Id, ScreenCode = screen, EntityType = "Customer", FieldName = "Email", CanView = false
        };

        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            db.FieldPermissions.AddRange(Row("SALES_CUSTOMERS"), Row("SALES_INVOICES"));
            await db.SaveChangesAsync();
        }

        await using var again = factory.CreateDirectDbContext(companyId);
        again.FieldPermissions.Add(Row("SALES_CUSTOMERS"));
        await Assert.ThrowsAsync<DbUpdateException>(() => again.SaveChangesAsync());
    }

    // ------------------------------------------------------------------------------ buttons

    [Fact]
    public async Task A_disabled_button_row_blocks_what_the_screen_permission_would_allow()
    {
        var companyId = await SeedCompanyAsync();
        var role = await SeedRoleAsync(companyId, new Rights("SALES_INVOICES", Approve: true));
        var user = Client(companyId, role.Code, "SALES_INVOICES");

        // No row: the screen's Approve lets the call through to the handler (no such invoice → 404).
        Assert.Equal(HttpStatusCode.NotFound, (await user.PostAsync("/api/v1/sales/invoices/999999999/post", null)).StatusCode);

        (await SetButtonsAsync(Client(companyId), role.Id, "SALES_INVOICES",
            new { screenCode = "SALES_INVOICES", buttonCode = "Post", isEnabled = false, requiresAuditLog = true })).EnsureSuccessStatusCode();

        var denied = await user.PostAsync("/api/v1/sales/invoices/999999999/post", null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("BUTTON-DENIED", await ErrorCodeAsync(denied));

        // Only that button: Reject still follows the screen permission.
        Assert.Equal(HttpStatusCode.NotFound, (await user.PostAsync("/api/v1/sales/invoices/999999999/reject", null)).StatusCode);
    }

    [Fact]
    public async Task An_enabled_button_row_grants_the_button_to_a_role_that_can_only_view_the_screen()
    {
        var companyId = await SeedCompanyAsync();
        var viewer = await SeedRoleAsync(companyId, new Rights("PURCHASING_PURCHASE_ORDERS"));
        var blind = await SeedRoleAsync(companyId);
        var user = Client(companyId, viewer.Code, "PURCHASING_PURCHASE_ORDERS");

        var before = await user.PostAsync("/api/v1/purchasing/purchase-orders/999999999/send", null);
        Assert.Equal("PERMISSION-DENIED", await ErrorCodeAsync(before));

        var admin = Client(companyId);
        var row = new { screenCode = "PURCHASING_PURCHASE_ORDERS", buttonCode = "Send", isEnabled = true, requiresAuditLog = true };
        (await SetButtonsAsync(admin, viewer.Id, "PURCHASING_PURCHASE_ORDERS", row)).EnsureSuccessStatusCode();
        (await SetButtonsAsync(admin, blind.Id, "PURCHASING_PURCHASE_ORDERS", row)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await user.PostAsync("/api/v1/purchasing/purchase-orders/999999999/send", null)).StatusCode);

        // A row never opens a screen the role cannot see.
        var blindUser = Client(companyId, blind.Code, "PURCHASING_PURCHASE_ORDERS");
        Assert.Equal("BUTTON-DENIED", await ErrorCodeAsync(await blindUser.PostAsync("/api/v1/purchasing/purchase-orders/999999999/send", null)));
    }

    [Fact]
    public async Task Unknown_buttons_are_refused_and_full_access_roles_cannot_be_given_rows()
    {
        var companyId = await SeedCompanyAsync();
        var role = await SeedRoleAsync(companyId);
        var admin = Client(companyId);

        var unknown = await SetButtonsAsync(admin, role.Id, "SALES_INVOICES",
            new { screenCode = "SALES_INVOICES", buttonCode = "Nope", isEnabled = true, requiresAuditLog = true });
        var otherScreen = await SetButtonsAsync(admin, role.Id, "SALES_INVOICES",
            new { screenCode = "SALES_INVOICES", buttonCode = "Hold", isEnabled = true, requiresAuditLog = true });

        Assert.Equal("SET-BUTTON-UNKNOWN", await ErrorCodeAsync(unknown));
        Assert.Equal("SET-BUTTON-UNKNOWN", await ErrorCodeAsync(otherScreen));

        await using var db = factory.CreateDirectDbContext(companyId);
        db.Roles.Add(new Role { CompanyId = companyId, Code = SystemRoles.CompanyAdmin, NameAr = "مدير", NameEn = "Admin", IsSystemRole = true });
        await db.SaveChangesAsync();
        var adminRoleId = await db.Roles.Where(r => r.Code == SystemRoles.CompanyAdmin).Select(r => r.Id).SingleAsync();
        var full = await SetButtonsAsync(admin, adminRoleId, "SALES_INVOICES",
            new { screenCode = "SALES_INVOICES", buttonCode = "Post", isEnabled = false, requiresAuditLog = true });
        Assert.Equal("SET-ROLE-FULL-ACCESS", await ErrorCodeAsync(full));
    }

    [Fact]
    public async Task Button_rows_are_unique_per_role_screen_and_button()
    {
        var companyId = await SeedCompanyAsync();
        var role = await SeedRoleAsync(companyId);
        ButtonPermission Row() => new() { CompanyId = companyId, RoleId = role.Id, ScreenCode = "SALES_INVOICES", ButtonCode = "Post" };

        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            db.ButtonPermissions.Add(Row());
            await db.SaveChangesAsync();
        }

        await using var again = factory.CreateDirectDbContext(companyId);
        again.ButtonPermissions.Add(Row());
        await Assert.ThrowsAsync<DbUpdateException>(() => again.SaveChangesAsync());
    }

    [Fact]
    public async Task The_role_button_view_reports_the_fallback_until_a_row_is_set()
    {
        var companyId = await SeedCompanyAsync();
        var role = await SeedRoleAsync(companyId, new Rights("POS_TABLE_BOARD", Edit: true));
        var admin = Client(companyId);

        JsonElement Button(JsonElement list, string code) => list.EnumerateArray().Single(b => b.GetProperty("buttonCode").GetString() == code);

        var before = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/permissions/role/{role.Id}/buttons?screenCode=POS_TABLE_BOARD");
        Assert.True(Button(before, "Hold").GetProperty("isEnabled").GetBoolean());      // Edit
        Assert.False(Button(before, "Cancel").GetProperty("isEnabled").GetBoolean());   // needs Approve
        Assert.False(Button(before, "Cancel").GetProperty("isConfigured").GetBoolean());

        (await SetButtonsAsync(admin, role.Id, "POS_TABLE_BOARD",
            new { screenCode = "POS_TABLE_BOARD", buttonCode = "Cancel", isEnabled = true, requiresAuditLog = false })).EnsureSuccessStatusCode();

        var after = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/permissions/role/{role.Id}/buttons?screenCode=POS_TABLE_BOARD");
        Assert.True(Button(after, "Cancel").GetProperty("isEnabled").GetBoolean());
        Assert.True(Button(after, "Cancel").GetProperty("isConfigured").GetBoolean());
        Assert.False(Button(after, "Cancel").GetProperty("requiresAuditLog").GetBoolean());
    }

    [Fact]
    public async Task The_catalog_endpoints_list_fields_and_buttons_per_screen()
    {
        var companyId = await SeedCompanyAsync();
        var admin = Client(companyId);

        var fieldScreens = await admin.GetFromJsonAsync<JsonElement>("/api/v1/permissions/field-catalog?screenCode=SALES_INVOICES");
        var invoice = fieldScreens.EnumerateArray().Single();
        var fields = invoice.GetProperty("fields").EnumerateArray().Select(f => $"{f.GetProperty("entityType").GetString()}.{f.GetProperty("fieldName").GetString()}").ToList();
        Assert.Contains("SalesInvoice.DiscountAmount", fields);
        Assert.Contains("Customer.Phone", fields);
        Assert.DoesNotContain("Supplier.Phone", fields);

        var buttonScreens = await admin.GetFromJsonAsync<JsonElement>("/api/v1/permissions/button-catalog?screenCode=POS_TABLE_BOARD");
        var buttons = buttonScreens.EnumerateArray().Single().GetProperty("buttons").EnumerateArray().ToList();
        Assert.True(buttons.Single(b => b.GetProperty("code").GetString() == "EditPrice").GetProperty("serverEnforced").GetBoolean());
        Assert.False(buttons.Single(b => b.GetProperty("code").GetString() == "Reprint").GetProperty("serverEnforced").GetBoolean());

        var all = await admin.GetFromJsonAsync<JsonElement>("/api/v1/permissions/button-catalog");
        Assert.Equal(ButtonPermissionCatalog.Buttons.Count, all.GetArrayLength());

        // The roles screen's permission guards them.
        var outsider = Client(companyId, (await SeedRoleAsync(companyId)).Code);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync("/api/v1/permissions/button-catalog")).StatusCode);
    }

    [Fact]
    public async Task Me_returns_field_and_button_permissions_per_screen()
    {
        var companyId = await SeedCompanyAsync();
        var role = await SeedRoleAsync(companyId, new Rights("SALES_INVOICES", Edit: true), new Rights("SALES_CUSTOMERS"));
        long userId;
        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            db.FieldPermissions.Add(new FieldPermission
            {
                CompanyId = companyId, RoleId = role.Id, ScreenCode = "SALES_INVOICES", EntityType = "Customer", FieldName = "Phone", CanView = false, CanEdit = false
            });
            var username = $"u{Guid.NewGuid():N}"[..14];
            var user = new User
            {
                Username = username, Email = $"{username}@test.local", FullName = "مستخدم", PasswordHash = "x", Status = UserStatus.Active,
                PasswordChangedAtUtc = DateTime.UtcNow
            };
            user.UserRoles.Add(new UserRole { RoleId = role.Id, AssignedAtUtc = DateTime.UtcNow, AssignedByUserId = 0 });
            user.UserScopes.Add(new UserScope { CompanyId = companyId, RoleInScope = role.Code, IsDefault = true, IsActive = true, GrantedAtUtc = DateTime.UtcNow });
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var me = await Client(companyId, role.Code, userId: userId).GetFromJsonAsync<JsonElement>("/api/v1/auth/me");

        var fields = me.GetProperty("fieldPermissions");
        Assert.False(fields.GetProperty("SALES_INVOICES").GetProperty("Customer").GetProperty("Phone").GetProperty("canView").GetBoolean());
        Assert.True(fields.GetProperty("SALES_CUSTOMERS").GetProperty("Customer").GetProperty("Phone").GetProperty("canView").GetBoolean());

        var buttons = me.GetProperty("buttonPermissions");
        Assert.False(buttons.GetProperty("SALES_INVOICES").GetProperty("Post").GetBoolean());               // needs Approve
        Assert.False(buttons.GetProperty("PURCHASING_PURCHASE_ORDERS").GetProperty("Send").GetBoolean());  // screen not granted
    }

    // ------------------------------------------------------------- button presses on the till

    private async Task<(CycleIds Ids, long CheckId, long LineId, (long Id, string Code) Role)> SeedCheckAsync()
    {
        var companyId = Random.Shared.NextInt64(1, long.MaxValue);
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var role = await SeedRoleAsync(companyId, new Rights("POS_TABLE_BOARD", Add: true, Edit: true, Delete: true));
        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(Client(companyId), ids);
        await using var db = factory.CreateDirectDbContext(companyId);
        var lineId = await db.CheckLines.Where(l => l.CheckId == checkId).Select(l => l.Id).SingleAsync();
        return (ids, checkId, lineId, role);
    }

    private static Task<HttpResponseMessage> UpdateLineAsync(HttpClient client, long checkId, long lineId, decimal quantity, decimal unitPrice) =>
        client.PutAsJsonAsync($"/api/v1/pos/checks/{checkId}/lines/{lineId}", new
        {
            quantity, unitPrice, discountAmount = 0m, isPriceManuallyOverridden = false, note = (string?)null
        });

    [Fact]
    public async Task A_successful_press_is_audited_unless_the_role_switched_it_off()
    {
        var s = await SeedCheckAsync();
        var companyId = s.Ids.CompanyId;
        var cashier = Client(companyId, s.Role.Code, "POS_TABLE_BOARD");

        (await cashier.PostAsync($"/api/v1/pos/checks/{s.CheckId}/hold", null)).EnsureSuccessStatusCode();

        (await SetButtonsAsync(Client(companyId), s.Role.Id, "POS_TABLE_BOARD",
            new { screenCode = "POS_TABLE_BOARD", buttonCode = "Hold", isEnabled = true, requiresAuditLog = false })).EnsureSuccessStatusCode();
        (await cashier.PostAsync($"/api/v1/pos/checks/{s.CheckId}/resume", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);
        var presses = await db.AuditLogs.Where(a => a.CompanyId == companyId && a.ActionType == AuditActionType.ButtonPress).ToListAsync();
        var press = Assert.Single(presses);
        Assert.Equal(s.CheckId, press.EntityId);
        Assert.Contains("\"button\":\"Hold\"", press.AdditionalData);
        Assert.Contains("\"action\":\"hold\"", press.AdditionalData);
    }

    [Fact]
    public async Task Changing_a_line_price_is_the_edit_price_button()
    {
        var s = await SeedCheckAsync();
        var companyId = s.Ids.CompanyId;
        (await SetButtonsAsync(Client(companyId), s.Role.Id, "POS_TABLE_BOARD",
            new { screenCode = "POS_TABLE_BOARD", buttonCode = "EditPrice", isEnabled = false, requiresAuditLog = true })).EnsureSuccessStatusCode();
        var cashier = Client(companyId, s.Role.Code, "POS_TABLE_BOARD");

        // Same price, more cups: an ordinary line edit.
        Assert.Equal(HttpStatusCode.NoContent, (await UpdateLineAsync(cashier, s.CheckId, s.LineId, 2m, 40m)).StatusCode);

        var priced = await UpdateLineAsync(cashier, s.CheckId, s.LineId, 2m, 30m);
        Assert.Equal("BUTTON-DENIED", await ErrorCodeAsync(priced));

        // Allowed (no row → Edit on the screen) and recorded.
        var admin = Client(companyId);
        (await SetButtonsAsync(admin, s.Role.Id, "POS_TABLE_BOARD")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await UpdateLineAsync(cashier, s.CheckId, s.LineId, 2m, 30m)).StatusCode);

        await using var db = factory.CreateDirectDbContext(companyId);
        Assert.Equal(30m, (await db.CheckLines.SingleAsync(l => l.Id == s.LineId)).UnitPrice);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.CompanyId == companyId && a.ActionType == AuditActionType.ButtonPress
                                                     && a.EntityId == s.LineId && a.AdditionalData!.Contains("EditPrice")));
    }

    [Fact]
    public async Task Voiding_a_line_the_kitchen_has_is_the_void_sent_line_button()
    {
        var s = await SeedCheckAsync();
        var companyId = s.Ids.CompanyId;
        (await SetButtonsAsync(Client(companyId), s.Role.Id, "POS_TABLE_BOARD",
            new { screenCode = "POS_TABLE_BOARD", buttonCode = "VoidSentLine", isEnabled = false, requiresAuditLog = true })).EnsureSuccessStatusCode();
        var cashier = Client(companyId, s.Role.Code, "POS_TABLE_BOARD");
        (await cashier.PostAsync($"/api/v1/pos/checks/{s.CheckId}/fire-to-kitchen", null)).EnsureSuccessStatusCode();

        HttpRequestMessage Void() => new(HttpMethod.Delete, $"/api/v1/pos/checks/{s.CheckId}/lines/{s.LineId}")
        {
            Content = JsonContent.Create(new { voidReason = "طلب خطأ" })
        };

        Assert.Equal("BUTTON-DENIED", await ErrorCodeAsync(await cashier.SendAsync(Void())));

        (await SetButtonsAsync(Client(companyId), s.Role.Id, "POS_TABLE_BOARD",
            new { screenCode = "POS_TABLE_BOARD", buttonCode = "VoidSentLine", isEnabled = true, requiresAuditLog = true })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await cashier.SendAsync(Void())).StatusCode);
    }

    // ------------------------------------------------------------------ catalog vs. endpoints

    [Fact]
    public void Every_button_endpoint_names_a_catalog_button_whose_fallback_is_the_endpoints_own_permission()
    {
        var actions = typeof(ScreenAttribute).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods().Select(m => (Controller: t, Method: m)))
            .Where(x => x.Method.GetCustomAttributes<ScreenButtonAttribute>().Any())
            .ToList();
        Assert.NotEmpty(actions);

        var used = new HashSet<(string, string)> { ("POS_TABLE_BOARD", "EditPrice"), ("POS_TABLE_BOARD", "VoidSentLine") };  // ButtonGuard
        foreach (var (controller, method) in actions)
        {
            var screens = controller.GetCustomAttribute<ScreenAttribute>(inherit: true)!.ScreenCodes;
            var route = controller.GetCustomAttribute<RouteAttribute>()!.Template;
            var http = method.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Single();
            var descriptor = new ControllerActionDescriptor
            {
                ControllerTypeInfo = controller.GetTypeInfo(),
                MethodInfo = method,
                AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo
                {
                    Template = string.IsNullOrEmpty(http.Template) ? route : $"{route}/{http.Template}"
                }
            };
            var (needed, _) = ScreenPermissionFilter.Required(descriptor, http.HttpMethods.Single());

            foreach (var button in method.GetCustomAttributes<ScreenButtonAttribute>())
            {
                var definition = ButtonPermissionCatalog.Find(button.ScreenCode, button.ButtonCode);
                Assert.True(definition is not null, $"{controller.Name}.{method.Name}: {button.ScreenCode}/{button.ButtonCode} is not in the catalog");
                Assert.Contains(button.ScreenCode, screens);
                Assert.True(definition!.FallbackAction == needed, $"{controller.Name}.{method.Name}: fallback {definition.FallbackAction} but the endpoint needs {needed}");
                used.Add((button.ScreenCode, button.ButtonCode));
            }
        }

        // Every button the server is said to enforce is enforced somewhere.
        var unenforced = ButtonPermissionCatalog.Buttons
            .SelectMany(s => s.Value.Where(b => b.ServerEnforced).Select(b => (s.Key, b.Code)))
            .Where(b => !used.Contains(b))
            .ToList();
        Assert.Empty(unenforced);
    }
}
