using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderLinesForInvoice;

/// <summary>
/// What an order still has left to bill, line by line (Remarks6) — the purchase invoice screen fills
/// itself from this the moment an order is picked, so nobody retypes quantities, units and prices
/// that are already agreed.
///
/// Only lines with something left are returned. When an invoice is being edited, its own lines are
/// excluded from "already billed" (via <paramref name="ExcludeInvoiceId"/>), so raising a line it
/// already owns is not counted against itself.
/// </summary>
public sealed record PurchaseOrderLineForInvoiceDto
{
    public required long PurchaseOrderLineId { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required long UnitId { get; init; }
    public required string UnitCode { get; init; }
    public required decimal UnitFactor { get; init; }
    public required decimal OrderedQuantity { get; init; }
    public required decimal InvoicedQuantity { get; init; }
    public required decimal RemainingQuantity { get; init; }
    public required decimal ReceivedQuantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public decimal? DiscountAmount { get; init; }
}

public sealed record GetPurchaseOrderLinesForInvoiceQuery(long PurchaseOrderId, long? ExcludeInvoiceId = null)
    : IRequest<IReadOnlyList<PurchaseOrderLineForInvoiceDto>>;

public sealed class GetPurchaseOrderLinesForInvoiceQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseOrderLinesForInvoiceQuery, IReadOnlyList<PurchaseOrderLineForInvoiceDto>>
{
    public async Task<IReadOnlyList<PurchaseOrderLineForInvoiceDto>> Handle(
        GetPurchaseOrderLinesForInvoiceQuery request, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.AsNoTracking()
                        .Include(o => o.Lines).ThenInclude(l => l.Item)
                        .Include(o => o.Lines).ThenInclude(l => l.Unit)
                        .FirstOrDefaultAsync(o => o.Id == request.PurchaseOrderId, cancellationToken)
                    ?? throw new NotFoundException(nameof(PurchaseOrder), request.PurchaseOrderId);

        if (order.Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Rejected)
        {
            throw new BusinessRuleException(
                "PUR-INVOICE-ORDER-NOT-BILLABLE", "أمر الشراء ده ملغي أو مرفوض — مينفعش يتفوتر.");
        }

        // The invoice being edited gives its own quantities back before we count what is left.
        var ownedByThisInvoice = request.ExcludeInvoiceId is { } invoiceId
            ? await db.PurchaseInvoiceLines.AsNoTracking()
                .Where(l => l.PurchaseInvoiceId == invoiceId && l.PurchaseOrderLineId != null)
                .Select(l => new { OrderLineId = l.PurchaseOrderLineId!.Value, l.BaseQuantity })
                .ToListAsync(cancellationToken)
            : [];

        return order.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l =>
            {
                var givenBack = ownedByThisInvoice
                    .Where(o => o.OrderLineId == l.Id)
                    .Sum(o => l.UnitFactor == 0 ? o.BaseQuantity : Math.Round(o.BaseQuantity / l.UnitFactor, 4));
                var invoiced = Math.Max(0m, l.InvoicedQuantity - givenBack);
                return new PurchaseOrderLineForInvoiceDto
                {
                    PurchaseOrderLineId = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    UnitId = l.UnitId,
                    UnitCode = l.Unit!.Code,
                    UnitFactor = l.UnitFactor,
                    OrderedQuantity = l.Quantity,
                    InvoicedQuantity = invoiced,
                    RemainingQuantity = Math.Max(0m, l.Quantity - invoiced),
                    ReceivedQuantity = l.ReceivedQuantity,
                    UnitPrice = l.UnitPrice,
                    DiscountAmount = l.DiscountAmount
                };
            })
            .Where(l => l.RemainingQuantity > 0)
            .ToList();
    }
}
