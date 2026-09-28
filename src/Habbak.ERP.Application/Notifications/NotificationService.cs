using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Notifications;

namespace Habbak.ERP.Application.Notifications;

public sealed class NotificationService(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext) : INotificationService
{
    public Task SendAsync(
        long recipientUserId, NotificationType type, string titleAr, string titleEn, string bodyAr, string bodyEn,
        bool requiresAction, string? relatedEntityType = null, long? relatedEntityId = null, CancellationToken cancellationToken = default) =>
        SendToMultipleAsync([recipientUserId], type, titleAr, titleEn, bodyAr, bodyEn, requiresAction, relatedEntityType, relatedEntityId, cancellationToken);

    public async Task SendToMultipleAsync(
        IEnumerable<long> recipientUserIds, NotificationType type, string titleAr, string titleEn, string bodyAr, string bodyEn,
        bool requiresAction, string? relatedEntityType = null, long? relatedEntityId = null, CancellationToken cancellationToken = default)
    {
        foreach (var recipientUserId in recipientUserIds.Distinct())
        {
            db.Notifications.Add(new Notification
            {
                CompanyId = currentCompanyContext.CompanyId,
                RecipientUserId = recipientUserId,
                Type = type,
                TitleAr = titleAr,
                TitleEn = titleEn,
                BodyAr = bodyAr,
                BodyEn = bodyEn,
                RequiresAction = requiresAction,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                IsRead = false
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
