using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrdersList;

public sealed class GetPurchaseOrdersListQuery : ListQuery, IRequest<PagedResult<PurchaseOrderListItemDto>>;

public sealed class GetPurchaseOrdersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseOrdersListQuery, PagedResult<PurchaseOrderListItemDto>>
{
    public async Task<PagedResult<PurchaseOrderListItemDto>> Handle(GetPurchaseOrdersListQuery request, CancellationToken cancellationToken)
    {
        var query = db.PurchaseOrders.AsNoTracking().Include(o => o.Supplier).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<PurchaseOrderStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            DateOnly? dateTerm = DateOnly.TryParse(term, out var parsedDate) ? parsedDate : null;

            query = query.Where(o =>
                o.OrderNumber.Contains(term) ||
                o.Supplier!.Code.Contains(term) ||
                o.Supplier!.NameAr.Contains(term) ||
                matchingStatuses.Contains(o.Status) ||
                (dateTerm != null && o.OrderDate == dateTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "orderDate" => descending ? query.OrderByDescending(o => o.OrderDate) : query.OrderBy(o => o.OrderDate),
            "status" => descending ? query.OrderByDescending(o => o.Status) : query.OrderBy(o => o.Status),
            _ => descending ? query.OrderByDescending(o => o.OrderNumber) : query.OrderBy(o => o.OrderNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new PurchaseOrderListItemDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                OrderDate = o.OrderDate,
                SupplierId = o.SupplierId,
                SupplierCode = o.Supplier!.Code,
                SupplierNameAr = o.Supplier!.NameAr,
                TotalAmount = o.TotalAmount,
                CurrencyCode = o.CurrencyCode,
                LineCount = o.Lines.Count,
                Status = o.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseOrderListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
