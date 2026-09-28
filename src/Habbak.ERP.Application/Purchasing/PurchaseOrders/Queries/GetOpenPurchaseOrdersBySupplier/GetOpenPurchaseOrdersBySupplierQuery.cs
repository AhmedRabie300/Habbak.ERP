using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders.Queries.GetOpenPurchaseOrdersBySupplier;

/// <summary>
/// The supplier's orders that still have something left to bill (Remarks6) — what the purchase
/// invoice screen offers once a supplier is picked.
///
/// "Still open" means at least one line whose InvoicedQuantity is below its Quantity, on an order
/// the supplier actually agreed to (Confirmed onwards) and that was not cancelled, rejected, closed
/// or archived. A fully billed order drops off the list by itself.
/// </summary>
public sealed record OpenPurchaseOrderDto
{
    public required long Id { get; init; }
    public required string OrderNumber { get; init; }
    public required DateOnly OrderDate { get; init; }
    public required string Status { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal TotalAmount { get; init; }
    public required int RemainingLineCount { get; init; }

    /// <summary>How much of the order is already billed, 0-100 — for the picker's label.</summary>
    public required decimal CompletionPercentage { get; init; }
}

public sealed record GetOpenPurchaseOrdersBySupplierQuery(long SupplierId) : IRequest<IReadOnlyList<OpenPurchaseOrderDto>>;

public sealed class GetOpenPurchaseOrdersBySupplierQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetOpenPurchaseOrdersBySupplierQuery, IReadOnlyList<OpenPurchaseOrderDto>>
{
    private static readonly PurchaseOrderStatus[] Billable =
    [
        PurchaseOrderStatus.Confirmed,
        PurchaseOrderStatus.PartiallyReceived,
        PurchaseOrderStatus.FullyReceived,
        PurchaseOrderStatus.PartiallyInvoiced
    ];

    public async Task<IReadOnlyList<OpenPurchaseOrderDto>> Handle(
        GetOpenPurchaseOrdersBySupplierQuery request, CancellationToken cancellationToken)
    {
        if (request.SupplierId <= 0)
        {
            throw new NotFoundException("Supplier", request.SupplierId);
        }

        var orders = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.SupplierId == request.SupplierId && Billable.Contains(o.Status))
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.OrderDate,
                o.Status,
                o.CurrencyCode,
                o.TotalAmount,
                Lines = o.Lines.Select(l => new { l.Quantity, l.InvoicedQuantity }).ToList()
            })
            .OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Id)
            .ToListAsync(cancellationToken);

        return orders
            .Select(o =>
            {
                var remaining = o.Lines.Count(l => l.InvoicedQuantity < l.Quantity);
                var ordered = o.Lines.Sum(l => l.Quantity);
                var invoiced = o.Lines.Sum(l => Math.Min(l.InvoicedQuantity, l.Quantity));
                return new OpenPurchaseOrderDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    OrderDate = o.OrderDate,
                    Status = o.Status.ToString(),
                    CurrencyCode = o.CurrencyCode,
                    TotalAmount = o.TotalAmount,
                    RemainingLineCount = remaining,
                    CompletionPercentage = ordered == 0 ? 0m : Math.Round(invoiced / ordered * 100m, 2)
                };
            })
            .Where(o => o.RemainingLineCount > 0)
            .ToList();
    }
}
