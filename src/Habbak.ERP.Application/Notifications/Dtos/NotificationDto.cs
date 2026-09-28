using Habbak.ERP.Domain.Notifications;

namespace Habbak.ERP.Application.Notifications.Dtos;

public sealed record NotificationDto(
    long Id, NotificationType Type, string TitleAr, string TitleEn, string BodyAr, string BodyEn,
    bool RequiresAction, string? RelatedEntityType, long? RelatedEntityId, bool IsRead, DateTime? ReadAtUtc, DateTime CreatedAtUtc);
