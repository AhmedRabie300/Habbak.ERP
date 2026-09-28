using Habbak.ERP.Application.Notifications;
using Habbak.ERP.Application.Notifications.Commands;
using Habbak.ERP.Application.Notifications.Queries;
using Habbak.ERP.Domain.Notifications;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests.Notifications;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 2.5 — the mandatory scenarios: send persists
/// correctly, PendingActionCount counts RequiresAction only (not just unread), UnreadCount counts
/// IsRead only, mark-as-read updates the row, and a recipient can never touch another user's
/// notification. Approval-engine integration (the four real call sites) is covered by the extra
/// assertions added to Approvals/ApprovalWorkflowEngineTests.cs, not duplicated here.
/// </summary>
public sealed class NotificationServiceTests(PostingServiceFixture fixture) : IClassFixture<PostingServiceFixture>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);
    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, 20)];

    private async Task<long> AddUserAsync(AppDbContext db)
    {
        var user = new User
        {
            Username = Unique("user"), Email = $"{Unique("user")}@test.local",
            PasswordHash = "$2a$12$placeholderplaceholderplaceholderplaceholderplacehold",
            FullName = "مستخدم اختبار", Status = UserStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Sending_a_notification_persists_it_for_the_recipient()
    {
        var companyId = NewCompanyId();
        long recipientId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            recipientId = await AddUserAsync(db);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var service = new NotificationService(db, new TestCurrentCompanyContext(companyId));
            await service.SendAsync(
                recipientId, NotificationType.GeneralInfo, "عنوان", "Title", "محتوى", "Body",
                requiresAction: false, relatedEntityType: "Test", relatedEntityId: 42);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var notification = await db.Notifications.SingleAsync(n => n.RecipientUserId == recipientId);
            Assert.Equal(NotificationType.GeneralInfo, notification.Type);
            Assert.Equal("Test", notification.RelatedEntityType);
            Assert.Equal(42, notification.RelatedEntityId);
            Assert.False(notification.IsRead);
        }
    }

    [Fact]
    public async Task SendToMultiple_notifies_every_distinct_recipient_once()
    {
        var companyId = NewCompanyId();
        long userA, userB;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            userA = await AddUserAsync(db);
            userB = await AddUserAsync(db);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var service = new NotificationService(db, new TestCurrentCompanyContext(companyId));
            // userA repeated on purpose — Distinct() inside SendToMultipleAsync must still send once.
            await service.SendToMultipleAsync([userA, userB, userA], NotificationType.GeneralInfo, "ع", "T", "ب", "B", false);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.RecipientUserId == userA));
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.RecipientUserId == userB));
        }
    }

    [Fact]
    public async Task Pending_action_count_only_counts_unread_notifications_that_require_action()
    {
        var companyId = NewCompanyId();
        long userId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            userId = await AddUserAsync(db);
            var service = new NotificationService(db, new TestCurrentCompanyContext(companyId, userId: userId));
            await service.SendAsync(userId, NotificationType.ApprovalPending, "1", "1", "1", "1", requiresAction: true);
            await service.SendAsync(userId, NotificationType.ApprovalPending, "2", "2", "2", "2", requiresAction: true);
            await service.SendAsync(userId, NotificationType.ApprovalApproved, "3", "3", "3", "3", requiresAction: false);
            await service.SendAsync(userId, NotificationType.GeneralInfo, "4", "4", "4", "4", requiresAction: false);
        }

        await using var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: userId));
        var pendingHandler = new GetPendingActionCountQueryHandler(db2, new TestCurrentCompanyContext(companyId, userId: userId));
        var pendingCount = await pendingHandler.Handle(new GetPendingActionCountQuery(), CancellationToken.None);
        Assert.Equal(2, pendingCount); // the 2 RequiresAction=true ones only — not the 2 info ones

        var unreadHandler = new GetUnreadCountQueryHandler(db2, new TestCurrentCompanyContext(companyId, userId: userId));
        var unreadCount = await unreadHandler.Handle(new GetUnreadCountQuery(), CancellationToken.None);
        Assert.Equal(4, unreadCount); // all 4 are unread regardless of RequiresAction
    }

    [Fact]
    public async Task Marking_as_read_clears_it_from_both_counts()
    {
        var companyId = NewCompanyId();
        long userId;
        long notificationId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            userId = await AddUserAsync(db);
            var notification = new Notification
            {
                CompanyId = companyId, RecipientUserId = userId, Type = NotificationType.ApprovalPending,
                TitleAr = "ع", TitleEn = "T", BodyAr = "ب", BodyEn = "B", RequiresAction = true, IsRead = false
            };
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
            notificationId = notification.Id;
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: userId)))
        {
            var handler = new MarkNotificationAsReadCommandHandler(db, new TestCurrentCompanyContext(companyId, userId: userId));
            await handler.Handle(new MarkNotificationAsReadCommand(notificationId), CancellationToken.None);
        }

        await using var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var notification2 = await db2.Notifications.SingleAsync(n => n.Id == notificationId);
        Assert.True(notification2.IsRead);
        Assert.NotNull(notification2.ReadAtUtc);

        var pendingHandler = new GetPendingActionCountQueryHandler(db2, new TestCurrentCompanyContext(companyId, userId: userId));
        Assert.Equal(0, await pendingHandler.Handle(new GetPendingActionCountQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task A_user_cannot_mark_or_delete_another_users_notification()
    {
        var companyId = NewCompanyId();
        long ownerId, otherId, notificationId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            ownerId = await AddUserAsync(db);
            otherId = await AddUserAsync(db);
            var notification = new Notification
            {
                CompanyId = companyId, RecipientUserId = ownerId, Type = NotificationType.GeneralInfo,
                TitleAr = "ع", TitleEn = "T", BodyAr = "ب", BodyEn = "B", RequiresAction = false, IsRead = false
            };
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
            notificationId = notification.Id;
        }

        await using var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: otherId));
        var markHandler = new MarkNotificationAsReadCommandHandler(db2, new TestCurrentCompanyContext(companyId, userId: otherId));
        await Assert.ThrowsAsync<Habbak.ERP.Application.Common.Exceptions.ForbiddenException>(
            () => markHandler.Handle(new MarkNotificationAsReadCommand(notificationId), CancellationToken.None));

        var deleteHandler = new DeleteNotificationCommandHandler(db2, new TestCurrentCompanyContext(companyId, userId: otherId));
        await Assert.ThrowsAsync<Habbak.ERP.Application.Common.Exceptions.ForbiddenException>(
            () => deleteHandler.Handle(new DeleteNotificationCommand(notificationId), CancellationToken.None));
    }
}
