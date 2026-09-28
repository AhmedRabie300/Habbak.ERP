using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Sales.DeliveryOrders.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.DeliveryOrders.Queries.GetDeliveryOrdersList;

public sealed class GetDeliveryOrdersListQuery : ListQuery, IRequest<PagedResult<DeliveryOrderListItemDto>>;

public sealed class GetDeliveryOrdersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDeliveryOrdersListQuery, PagedResult<DeliveryOrderListItemDto>>
{
    public async Task<PagedResult<DeliveryOrderListItemDto>> Handle(GetDeliveryOrdersListQuery request, CancellationToken cancellationToken)
    {
        var query = db.DeliveryOrders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<DeliveryOrderStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            query = query.Where(d =>
                d.DeliveryNumber.Contains(term) ||
                d.Customer!.NameAr.Contains(term) ||
                matchingStatuses.Contains(d.Status));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "deliveryDate" => descending ? query.OrderByDescending(d => d.DeliveryDate) : query.OrderBy(d => d.DeliveryDate),
            "status" => descending ? query.OrderByDescending(d => d.Status) : query.OrderBy(d => d.Status),
            _ => descending ? query.OrderByDescending(d => d.DeliveryNumber) : query.OrderBy(d => d.DeliveryNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DeliveryOrderListItemDto
            {
                Id = d.Id,
                DeliveryNumber = d.DeliveryNumber,
                DeliveryDate = d.DeliveryDate,
                BranchId = d.BranchId,
                CustomerId = d.CustomerId,
                CustomerNameAr = d.Customer!.NameAr,
                WarehouseId = d.WarehouseId,
                WarehouseNameAr = d.Warehouse!.NameAr,
                SourceOrderId = d.SourceOrderId,
                SourceInvoiceId = d.SourceInvoiceId,
                LineCount = d.Lines.Count,
                Status = d.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<DeliveryOrderListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
