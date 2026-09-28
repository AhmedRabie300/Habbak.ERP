using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Notifications.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Notifications.Queries;

public enum NotificationFilter { All = 1, PendingAction = 2, Unread = 3 }

public sealed record GetMyNotificationsQuery(NotificationFilter Filter, int Page = 1, int PageSize = 25) : IRequest<PagedResult<NotificationDto>>;

public sealed class GetMyNotificationsQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<GetMyNotificationsQuery, PagedResult<NotificationDto>>
{
    public async Task<PagedResult<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentCompanyContext.UserId;
        var query = db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == userId);

        query = request.Filter switch
        {
            NotificationFilter.PendingAction => query.Where(n => n.RequiresAction),
            NotificationFilter.Unread => query.Where(n => !n.IsRead),
            _ => query
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var pageSize = request.PageSize is > 0 and <= 200 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(
                n.Id, n.Type, n.TitleAr, n.TitleEn, n.BodyAr, n.BodyEn, n.RequiresAction,
                n.RelatedEntityType, n.RelatedEntityId, n.IsRead, n.ReadAtUtc, n.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }
}

public sealed record GetUnreadCountQuery : IRequest<int>;

public sealed class GetUnreadCountQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext) : IRequestHandler<GetUnreadCountQuery, int>
{
    public Task<int> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var userId = currentCompanyContext.UserId;
        return db.Notifications.AsNoTracking().CountAsync(n => n.RecipientUserId == userId && !n.IsRead, cancellationToken);
    }
}

/// <summary>The bell's badge — Docs/Implementation/Phase-2.5-Research.md: counts RequiresAction, not IsRead.</summary>
public sealed record GetPendingActionCountQuery : IRequest<int>;

public sealed class GetPendingActionCountQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext) : IRequestHandler<GetPendingActionCountQuery, int>
{
    public Task<int> Handle(GetPendingActionCountQuery request, CancellationToken cancellationToken)
    {
        var userId = currentCompanyContext.UserId;
        return db.Notifications.AsNoTracking().CountAsync(n => n.RecipientUserId == userId && n.RequiresAction && !n.IsRead, cancellationToken);
    }
}
