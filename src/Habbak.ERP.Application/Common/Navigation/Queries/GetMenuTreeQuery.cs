using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Navigation.Dtos;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Common.Navigation.Queries;

/// <summary>
/// GET /api/v1/navigation/menu (00-System-Wide-Corrections-01.md, section 3.2) — only the screens
/// the user may view; a group left with nothing to show is dropped.
/// </summary>
public sealed record GetMenuTreeQuery(string Lang) : IRequest<List<MenuTreeNodeDto>>;

public sealed class GetMenuTreeQueryHandler(IApplicationDbContext db, IUserAccessService access) : IRequestHandler<GetMenuTreeQuery, List<MenuTreeNodeDto>>
{
    public async Task<List<MenuTreeNodeDto>> Handle(GetMenuTreeQuery request, CancellationToken cancellationToken)
    {
        var items = await db.MenuItems
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync(cancellationToken);

        var rights = await access.GetCurrentAsync(cancellationToken);
        items = items.Where(m => string.IsNullOrEmpty(m.RouteKey) || rights.For(m.Code).View).ToList();

        var useEnglish = string.Equals(request.Lang, "en", StringComparison.OrdinalIgnoreCase);
        var byParent = items.ToLookup(m => m.ParentId);

        MenuTreeNodeDto ToDto(MenuItem m) => new()
        {
            Id = m.Id,
            Code = m.Code,
            Name = useEnglish ? m.NameEn : m.NameAr,
            RouteKey = m.RouteKey,
            IconKey = m.IconKey
        };

        List<MenuTreeNodeDto> Build(long? parentId) =>
            byParent[parentId]
                .Select(m =>
                {
                    var dto = ToDto(m);
                    dto.Children = Build(m.Id);
                    return dto;
                })
                .Where(dto => !string.IsNullOrEmpty(dto.RouteKey) || dto.Children.Count > 0)
                .ToList();

        return Build(null);
    }
}
