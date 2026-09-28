using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Inventory.InventoryCounts.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.InventoryCounts.Queries.GetInventoryCountsList;

public sealed class GetInventoryCountsListQuery : ListQuery, IRequest<PagedResult<InventoryCountListItemDto>>;

public sealed class GetInventoryCountsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetInventoryCountsListQuery, PagedResult<InventoryCountListItemDto>>
{
    public async Task<PagedResult<InventoryCountListItemDto>> Handle(GetInventoryCountsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.InventoryCounts.AsNoTracking().Include(c => c.Warehouse).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<InventoryCountStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            DateOnly? dateTerm = DateOnly.TryParse(term, out var parsedDate) ? parsedDate : null;

            query = query.Where(c =>
                c.CountNumber.Contains(term) ||
                matchingStatuses.Contains(c.Status) ||
                (dateTerm != null && c.CountDate == dateTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "countDate" => descending ? query.OrderByDescending(c => c.CountDate) : query.OrderBy(c => c.CountDate),
            "status" => descending ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            _ => descending ? query.OrderByDescending(c => c.CountNumber) : query.OrderBy(c => c.CountNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new InventoryCountListItemDto
            {
                Id = c.Id,
                CountNumber = c.CountNumber,
                CountDate = c.CountDate,
                WarehouseId = c.WarehouseId,
                WarehouseCode = c.Warehouse!.Code,
                CountType = c.CountType.ToString(),
                Status = c.Status.ToString(),
                LineCount = c.Lines.Count
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryCountListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
