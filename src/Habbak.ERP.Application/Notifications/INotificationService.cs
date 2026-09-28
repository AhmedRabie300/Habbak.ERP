using Habbak.ERP.Domain.Notifications;

namespace Habbak.ERP.Application.Notifications;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 2.5 — the one write path into the Notification
/// table. Callers are ordinary command handlers calling this directly (Docs/Implementation/
/// Phase-2.5-Research.md §1.6 — "in-process, no MediatR handlers": a real domain-event/handler
/// indirection would be over-engineering for four call sites). Never call SaveChangesAsync before
/// this in the same handler if you want the business mutation and the notification to commit
/// together — this service saves the whole change tracker, not just its own rows, so calling it as
/// the last thing a handler does is what makes it atomic with everything the handler already changed.
/// </summary>
public interface INotificationService
{
    Task SendAsync(
        long recipientUserId, NotificationType type, string titleAr, string titleEn, string bodyAr, string bodyEn,
        bool requiresAction, string? relatedEntityType = null, long? relatedEntityId = null, CancellationToken cancellationToken = default);

    Task SendToMultipleAsync(
        IEnumerable<long> recipientUserIds, NotificationType type, string titleAr, string titleEn, string bodyAr, string bodyEn,
        bool requiresAction, string? relatedEntityType = null, long? relatedEntityId = null, CancellationToken cancellationToken = default);
}
