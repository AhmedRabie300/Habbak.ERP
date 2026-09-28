using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Notifications.Commands;

public sealed record MarkNotificationAsReadCommand(long Id) : IRequest;

public sealed class MarkNotificationAsReadCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<MarkNotificationAsReadCommand>
{
    public async Task Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Notification), request.Id);

        // A recipient's own notifications only — never another user's, even within the same company.
        if (notification.RecipientUserId != currentCompanyContext.UserId)
        {
            throw new ForbiddenException("NOTIFICATION-NOT-YOURS", "الإشعار ده مش بتاعك.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed record MarkAllNotificationsAsReadCommand : IRequest;

public sealed class MarkAllNotificationsAsReadCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<MarkAllNotificationsAsReadCommand>
{
    public async Task Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = currentCompanyContext.UserId;
        var unread = await db.Notifications.Where(n => n.RecipientUserId == userId && !n.IsRead).ToListAsync(cancellationToken);

        if (unread.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record DeleteNotificationCommand(long Id) : IRequest;

public sealed class DeleteNotificationCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<DeleteNotificationCommand>
{
    public async Task Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Notification), request.Id);

        if (notification.RecipientUserId != currentCompanyContext.UserId)
        {
            throw new ForbiddenException("NOTIFICATION-NOT-YOURS", "الإشعار ده مش بتاعك.");
        }

        db.Notifications.Remove(notification);
        await db.SaveChangesAsync(cancellationToken);
    }
}
