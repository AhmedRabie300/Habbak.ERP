using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habbak.ERP.API.Auth;
using Habbak.ERP.Domain.Notifications;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 2.5 — /api/v1/notifications. [AnySignedInUser]
/// (Docs/Implementation/Phase-2.5-Research.md §1.7): any signed-in user reaches their own
/// notifications with no screen-permission grant needed, but never another user's.
/// </summary>
public class NotificationsApiTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
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

    private async Task<long> SeedNotificationAsync(Seeded company, long recipientUserId, NotificationType type, bool requiresAction, bool isRead = false)
    {
        await using var db = factory.CreateDirectDbContext(company.CompanyId);
        var notification = new Notification
        {
            CompanyId = company.CompanyId, RecipientUserId = recipientUserId, Type = type,
            TitleAr = "ع", TitleEn = "T", BodyAr = "ب", BodyEn = "B", RequiresAction = requiresAction, IsRead = isRead
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        return notification.Id;
    }

    private HttpClient Client(long companyId, long userId, string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

    [Fact]
    public async Task Any_signed_in_user_reaches_their_own_notifications_with_no_screen_grant()
    {
        var company = await SeedCompanyAsync();
        var userId = await SeedUserAsync(company, SystemRoles.Cashier); // no screen permissions granted at all
        await SeedNotificationAsync(company, userId, NotificationType.ApprovalPending, requiresAction: true);
        await SeedNotificationAsync(company, userId, NotificationType.ApprovalApproved, requiresAction: false, isRead: true);

        var client = Client(company.CompanyId, userId, SystemRoles.Cashier);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        Assert.Equal(2, list.GetProperty("totalCount").GetInt32());

        var pending = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/pending-action-count");
        Assert.Equal(1, pending.GetProperty("count").GetInt32());

        var unread = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count");
        Assert.Equal(1, unread.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task List_filters_by_pending_action_and_unread_independently()
    {
        var company = await SeedCompanyAsync();
        var userId = await SeedUserAsync(company, SystemRoles.Cashier);
        await SeedNotificationAsync(company, userId, NotificationType.ApprovalPending, requiresAction: true);      // pending action, unread
        await SeedNotificationAsync(company, userId, NotificationType.ApprovalApproved, requiresAction: false);    // info, unread
        await SeedNotificationAsync(company, userId, NotificationType.GeneralInfo, requiresAction: false, isRead: true); // info, read

        var client = Client(company.CompanyId, userId, SystemRoles.Cashier);

        var pendingOnly = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications?filter=PendingAction");
        Assert.Equal(1, pendingOnly.GetProperty("totalCount").GetInt32());

        var unreadOnly = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications?filter=Unread");
        Assert.Equal(2, unreadOnly.GetProperty("totalCount").GetInt32());

        var all = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications?filter=All");
        Assert.Equal(3, all.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Mark_as_read_and_delete_work_and_are_reflected_in_the_counts()
    {
        var company = await SeedCompanyAsync();
        var userId = await SeedUserAsync(company, SystemRoles.Cashier);
        var toRead = await SeedNotificationAsync(company, userId, NotificationType.ApprovalPending, requiresAction: true);
        var toDelete = await SeedNotificationAsync(company, userId, NotificationType.GeneralInfo, requiresAction: false);

        var client = Client(company.CompanyId, userId, SystemRoles.Cashier);

        var markRead = await client.PostAsync($"/api/v1/notifications/{toRead}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, markRead.StatusCode);

        var pendingAfterRead = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/pending-action-count");
        Assert.Equal(0, pendingAfterRead.GetProperty("count").GetInt32());

        var delete = await client.DeleteAsync($"/api/v1/notifications/{toDelete}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var listAfter = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        Assert.Equal(1, listAfter.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task A_user_cannot_read_or_delete_another_users_notification()
    {
        var company = await SeedCompanyAsync();
        var ownerId = await SeedUserAsync(company, SystemRoles.Cashier);
        var otherId = await SeedUserAsync(company, SystemRoles.Cashier);
        var notificationId = await SeedNotificationAsync(company, ownerId, NotificationType.GeneralInfo, requiresAction: false);

        var other = Client(company.CompanyId, otherId, SystemRoles.Cashier);

        var markRead = await other.PostAsync($"/api/v1/notifications/{notificationId}/read", null);
        Assert.Equal(HttpStatusCode.Forbidden, markRead.StatusCode);

        var delete = await other.DeleteAsync($"/api/v1/notifications/{notificationId}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Mark_all_as_read_clears_every_unread_notification_for_the_caller_only()
    {
        var company = await SeedCompanyAsync();
        var userId = await SeedUserAsync(company, SystemRoles.Cashier);
        var otherId = await SeedUserAsync(company, SystemRoles.Cashier);
        await SeedNotificationAsync(company, userId, NotificationType.ApprovalPending, requiresAction: true);
        await SeedNotificationAsync(company, userId, NotificationType.ApprovalApproved, requiresAction: false);
        await SeedNotificationAsync(company, otherId, NotificationType.GeneralInfo, requiresAction: false);

        var client = Client(company.CompanyId, userId, SystemRoles.Cashier);
        var markAll = await client.PostAsync("/api/v1/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.NoContent, markAll.StatusCode);

        var unread = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count");
        Assert.Equal(0, unread.GetProperty("count").GetInt32());

        var otherClient = Client(company.CompanyId, otherId, SystemRoles.Cashier);
        var otherUnread = await otherClient.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count");
        Assert.Equal(1, otherUnread.GetProperty("count").GetInt32()); // untouched by userId's mark-all-read
    }
}
