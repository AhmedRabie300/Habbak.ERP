using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Sales.SalesOrders.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesOrders.Queries.GetSalesOrdersList;

public sealed class GetSalesOrdersListQuery : ListQuery, IRequest<PagedResult<SalesOrderListItemDto>>;

public sealed class GetSalesOrdersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSalesOrdersListQuery, PagedResult<SalesOrderListItemDto>>
{
    public async Task<PagedResult<SalesOrderListItemDto>> Handle(GetSalesOrdersListQuery request, CancellationToken cancellationToken)
    {
        var query = db.SalesOrders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<SalesOrderStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            query = query.Where(o =>
                o.OrderNumber.Contains(term) ||
                o.Customer!.NameAr.Contains(term) ||
                matchingStatuses.Contains(o.Status));
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
            .Select(o => new SalesOrderListItemDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                OrderDate = o.OrderDate,
                BranchId = o.BranchId,
                CustomerId = o.CustomerId,
                CustomerNameAr = o.Customer!.NameAr,
                SourceQuoteId = o.SourceQuoteId,
                Subtotal = o.Subtotal,
                Status = o.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SalesOrderListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
