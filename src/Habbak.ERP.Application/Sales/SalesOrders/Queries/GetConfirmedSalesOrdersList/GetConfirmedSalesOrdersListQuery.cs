using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesOrders.Queries.GetConfirmedSalesOrdersList;

public sealed class ConfirmedSalesOrderDto
{
    public required long Id { get; init; }
    public required string OrderNumber { get; init; }
    public required long CustomerId { get; init; }
    public required string CustomerNameAr { get; init; }
    public required decimal Subtotal { get; init; }
}

/// <summary>Feeds screen #7's "convert to sales invoice" picker — only Confirmed (or already
/// partially/fully delivered, once that's reachable) orders are offered.</summary>
public sealed record GetConfirmedSalesOrdersListQuery : IRequest<IReadOnlyList<ConfirmedSalesOrderDto>>;

public sealed class GetConfirmedSalesOrdersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetConfirmedSalesOrdersListQuery, IReadOnlyList<ConfirmedSalesOrderDto>>
{
    public async Task<IReadOnlyList<ConfirmedSalesOrderDto>> Handle(GetConfirmedSalesOrdersListQuery request, CancellationToken cancellationToken)
    {
        return await db.SalesOrders
            .AsNoTracking()
            .Where(o => o.Status == SalesOrderStatus.Confirmed || o.Status == SalesOrderStatus.PartiallyDelivered || o.Status == SalesOrderStatus.Delivered)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new ConfirmedSalesOrderDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                CustomerId = o.CustomerId,
                CustomerNameAr = o.Customer!.NameAr,
                Subtotal = o.Subtotal
            })
            .ToListAsync(cancellationToken);
    }
}
