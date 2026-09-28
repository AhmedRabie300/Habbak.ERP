using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Purchasing.GoodsReceipts.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.GoodsReceipts.Queries.GetGoodsReceiptsList;

public sealed class GetGoodsReceiptsListQuery : ListQuery, IRequest<PagedResult<GoodsReceiptListItemDto>>;

public sealed class GetGoodsReceiptsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetGoodsReceiptsListQuery, PagedResult<GoodsReceiptListItemDto>>
{
    public async Task<PagedResult<GoodsReceiptListItemDto>> Handle(GetGoodsReceiptsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.GoodsReceipts.AsNoTracking().Include(r => r.Warehouse).Include(r => r.Supplier).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<GoodsReceiptStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            DateOnly? dateTerm = DateOnly.TryParse(term, out var parsedDate) ? parsedDate : null;

            query = query.Where(r =>
                r.ReceiptNumber.Contains(term) ||
                r.Supplier!.Code.Contains(term) ||
                r.Supplier!.NameAr.Contains(term) ||
                matchingStatuses.Contains(r.Status) ||
                (dateTerm != null && r.ReceiptDate == dateTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "receiptDate" => descending ? query.OrderByDescending(r => r.ReceiptDate) : query.OrderBy(r => r.ReceiptDate),
            "status" => descending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            _ => descending ? query.OrderByDescending(r => r.ReceiptNumber) : query.OrderBy(r => r.ReceiptNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new GoodsReceiptListItemDto
            {
                Id = r.Id,
                ReceiptNumber = r.ReceiptNumber,
                ReceiptDate = r.ReceiptDate,
                WarehouseId = r.WarehouseId,
                WarehouseCode = r.Warehouse!.Code,
                SupplierId = r.SupplierId,
                SupplierCode = r.Supplier!.Code,
                SupplierNameAr = r.Supplier!.NameAr,
                LineCount = r.Lines.Count,
                Status = r.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<GoodsReceiptListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
