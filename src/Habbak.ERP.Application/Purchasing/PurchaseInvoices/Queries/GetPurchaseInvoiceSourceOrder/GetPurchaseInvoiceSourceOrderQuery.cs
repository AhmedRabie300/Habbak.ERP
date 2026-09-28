using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseInvoices.Queries.GetPurchaseInvoiceSourceOrder;

/// <summary>
/// The purchase order behind an invoice, line by line (Remarks4, item 5): what was ordered, what
/// actually arrived, what has already been billed, and what is left. Until now the invoice screen
/// named its order and stopped there, so nobody could tell a supplier was billing for goods that
/// never turned up.
///
/// Received quantities come from the posted goods receipts of that order — the accepted quantity,
/// not what the delivery note claimed (confirmed in Remarks4, section 6, question 2). Everything is
/// in base units so an order in cartons and an invoice in pieces still compare.
/// </summary>
public sealed record PurchaseOrderLineFulfillmentDto
{
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal OrderedQuantity { get; init; }
    public required string OrderedUnitCode { get; init; }
    public required decimal OrderedBaseQuantity { get; init; }
    public required decimal ReceivedBaseQuantity { get; init; }
    public required decimal InvoicedOnThisInvoiceBaseQuantity { get; init; }
    public required decimal InvoicedElsewhereBaseQuantity { get; init; }
    public required decimal RemainingBaseQuantity { get; init; }
    public required decimal OrderedUnitPrice { get; init; }

    /// <summary>What this invoice charges per base unit, so a price that drifted from the order is visible.</summary>
    public decimal? InvoicedBaseUnitCost { get; init; }
}

public sealed record PurchaseInvoiceSourceOrderDto
{
    public required long PurchaseOrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required DateOnly OrderDate { get; init; }
    public required string Status { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal TotalAmount { get; init; }
    public DateOnly? ExpectedDeliveryDate { get; init; }
    public string? PurchaseRequestNumber { get; init; }
    public required IReadOnlyList<PurchaseOrderLineFulfillmentDto> Lines { get; init; }
}

/// <summary>Null when the invoice was raised without an order (the "invoice only" cycle).</summary>
public sealed record GetPurchaseInvoiceSourceOrderQuery(long InvoiceId) : IRequest<PurchaseInvoiceSourceOrderDto?>;

public sealed class GetPurchaseInvoiceSourceOrderQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseInvoiceSourceOrderQuery, PurchaseInvoiceSourceOrderDto?>
{
    public async Task<PurchaseInvoiceSourceOrderDto?> Handle(GetPurchaseInvoiceSourceOrderQuery request, CancellationToken cancellationToken)
    {
        var invoice = await db.PurchaseInvoices.AsNoTracking()
                          .Where(i => i.Id == request.InvoiceId)
                          .Select(i => new { i.Id, i.PurchaseOrderId })
                          .FirstOrDefaultAsync(cancellationToken)
                      ?? throw new NotFoundException(nameof(PurchaseInvoice), request.InvoiceId);

        if (invoice.PurchaseOrderId is not { } orderId)
        {
            return null;
        }

        var order = await db.PurchaseOrders.AsNoTracking()
                        .Include(o => o.PurchaseRequest)
                        .Include(o => o.Lines).ThenInclude(l => l.Item)
                        .Include(o => o.Lines).ThenInclude(l => l.Unit)
                        .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
                    ?? throw new NotFoundException(nameof(PurchaseOrder), orderId);

        // What arrived: accepted quantities on this order's posted receipts.
        var received = await db.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.GoodsReceipt!.PurchaseOrderId == orderId && l.GoodsReceipt.Status == GoodsReceiptStatus.Posted)
            .GroupBy(l => l.ItemId)
            .Select(g => new { ItemId = g.Key, BaseQuantity = g.Sum(l => l.AcceptedQuantity * l.UnitFactor) })
            .ToDictionaryAsync(x => x.ItemId, x => x.BaseQuantity, cancellationToken);

        // What has been billed: every invoice of this order that was not cancelled.
        var invoiced = await db.PurchaseInvoiceLines.AsNoTracking()
            .Where(l => l.PurchaseInvoice!.PurchaseOrderId == orderId && l.PurchaseInvoice.Status != PurchaseInvoiceStatus.Cancelled)
            .Select(l => new { l.PurchaseInvoiceId, l.ItemId, l.BaseQuantity, l.BaseUnitCost })
            .ToListAsync(cancellationToken);

        var lines = order.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l =>
            {
                var onThis = invoiced.Where(i => i.PurchaseInvoiceId == invoice.Id && i.ItemId == l.ItemId).ToList();
                var elsewhere = invoiced.Where(i => i.PurchaseInvoiceId != invoice.Id && i.ItemId == l.ItemId).Sum(i => i.BaseQuantity);
                var onThisQuantity = onThis.Sum(i => i.BaseQuantity);
                return new PurchaseOrderLineFulfillmentDto
                {
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    OrderedQuantity = l.Quantity,
                    OrderedUnitCode = l.Unit!.Code,
                    OrderedBaseQuantity = l.BaseQuantity,
                    ReceivedBaseQuantity = received.GetValueOrDefault(l.ItemId),
                    InvoicedOnThisInvoiceBaseQuantity = onThisQuantity,
                    InvoicedElsewhereBaseQuantity = elsewhere,
                    RemainingBaseQuantity = Math.Max(0m, l.BaseQuantity - onThisQuantity - elsewhere),
                    OrderedUnitPrice = l.UnitPrice,
                    InvoicedBaseUnitCost = onThis.Count == 0 ? null : onThis[0].BaseUnitCost
                };
            })
            .ToList();

        return new PurchaseInvoiceSourceOrderDto
        {
            PurchaseOrderId = order.Id,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            Status = order.Status.ToString(),
            CurrencyCode = order.CurrencyCode,
            TotalAmount = order.TotalAmount,
            ExpectedDeliveryDate = order.ExpectedDeliveryDate,
            PurchaseRequestNumber = order.PurchaseRequest?.RequestNumber,
            Lines = lines
        };
    }
}
