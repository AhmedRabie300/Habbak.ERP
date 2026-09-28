using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.GoodsReceipts.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders.Queries.GetPostablePurchaseOrdersList;

/// <summary>Feeds screen #6's create form — Confirmed/PartiallyReceived orders, with each line's
/// still-outstanding quantity (Quantity − ReceivedQuantity) so the receipt screen can pre-fill a
/// sensible default and flag when a line is already fully received.</summary>
public sealed record GetPostablePurchaseOrdersListQuery : IRequest<IReadOnlyList<PostablePurchaseOrderDto>>;

public sealed class GetPostablePurchaseOrdersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPostablePurchaseOrdersListQuery, IReadOnlyList<PostablePurchaseOrderDto>>
{
    public async Task<IReadOnlyList<PostablePurchaseOrderDto>> Handle(
        GetPostablePurchaseOrdersListQuery request, CancellationToken cancellationToken)
    {
        var orders = await db.PurchaseOrders
            .AsNoTracking()
            .Where(o => o.Status == PurchaseOrderStatus.Confirmed || o.Status == PurchaseOrderStatus.PartiallyReceived
                || o.Status == PurchaseOrderStatus.PartiallyInvoiced || o.Status == PurchaseOrderStatus.Invoiced)
            .Include(o => o.Supplier)
            .Include(o => o.Lines).ThenInclude(l => l.Item)
            .Include(o => o.Lines).ThenInclude(l => l.Unit)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync(cancellationToken);

        return orders
            .Select(o => new PostablePurchaseOrderDto(
                o.Id,
                o.OrderNumber,
                o.SupplierId,
                o.Supplier!.Code,
                o.Lines
                    .Where(l => l.Quantity - l.ReceivedQuantity > 0)
                    .Select(l => new PostableOrderLineDto(l.ItemId, l.Item!.Code, l.Item!.NameAr, l.Quantity - l.ReceivedQuantity, l.UnitId, l.Unit!.Code))
                    .ToList()))
            .Where(o => o.Lines.Count > 0)
            .ToList();
    }
}
