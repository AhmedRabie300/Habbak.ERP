using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Inventory.WasteRecords.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.WasteRecords.Queries.GetWasteRecordsList;

public sealed class GetWasteRecordsListQuery : ListQuery, IRequest<PagedResult<WasteRecordListItemDto>>;

public sealed class GetWasteRecordsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetWasteRecordsListQuery, PagedResult<WasteRecordListItemDto>>
{
    public async Task<PagedResult<WasteRecordListItemDto>> Handle(GetWasteRecordsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.WasteRecords.AsNoTracking().Include(w => w.Warehouse).Include(w => w.Item).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(w =>
                w.Item!.Code.Contains(term) ||
                w.Item!.NameAr.Contains(term) ||
                w.Reason.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "quantity" => descending ? query.OrderByDescending(w => w.Quantity) : query.OrderBy(w => w.Quantity),
            _ => descending ? query.OrderByDescending(w => w.WasteDate) : query.OrderBy(w => w.WasteDate)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new WasteRecordListItemDto
            {
                Id = w.Id,
                WarehouseId = w.WarehouseId,
                WarehouseCode = w.Warehouse!.Code,
                ItemId = w.ItemId,
                ItemCode = w.Item!.Code,
                ItemNameAr = w.Item!.NameAr,
                Quantity = w.Quantity,
                WasteDate = w.WasteDate,
                Reason = w.Reason,
                SourceDocumentType = w.SourceDocumentType
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<WasteRecordListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
