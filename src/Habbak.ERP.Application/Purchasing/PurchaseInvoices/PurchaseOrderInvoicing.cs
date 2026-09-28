using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseInvoices;

/// <summary>
/// Keeps a purchase order's "how much of this has been billed" in step with its invoices (Remarks6).
///
/// Every invoice line that came off an order line carries <see cref="PurchaseInvoiceLine.PurchaseOrderLineId"/>;
/// saving the invoice adds its quantity to that order line's <see cref="PurchaseOrderLine.InvoicedQuantity"/>,
/// editing it takes the old quantities back off first, and cancelling it returns them all. The order's
/// status follows: nothing billed leaves it alone, some of it billed makes it PartiallyInvoiced, all of
/// it billed makes it Invoiced.
///
/// Quantities are compared in the **order line's own unit**: an invoice line in a different unit is
/// converted through the two lines' unit factors, so billing "2 bags" against an order in grams counts
/// as the 2 000 g it is.
/// </summary>
internal static class PurchaseOrderInvoicing
{
    /// <summary>Loads the order lines an invoice's lines point at, locked to the same context.</summary>
    public static async Task<Dictionary<long, PurchaseOrderLine>> LoadOrderLinesAsync(
        IApplicationDbContext db, IEnumerable<long> orderLineIds, CancellationToken cancellationToken)
    {
        var ids = orderLineIds.Distinct().ToList();
        return ids.Count == 0
            ? []
            : await db.PurchaseOrderLines.Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);
    }

    /// <summary>The invoice line's quantity expressed in its order line's unit.</summary>
    public static decimal InOrderLineUnit(decimal baseQuantity, PurchaseOrderLine orderLine) =>
        orderLine.UnitFactor == 0 ? baseQuantity : Math.Round(baseQuantity / orderLine.UnitFactor, 4);

    /// <summary>
    /// Checks every line of an invoice against the order it bills, then returns what to add to each
    /// order line. Throws before anything is written, so a refused invoice changes nothing.
    /// </summary>
    public static async Task<Dictionary<long, decimal>> PlanAsync(
        IApplicationDbContext db,
        PurchaseInvoice invoice,
        IReadOnlyCollection<PurchaseInvoiceLine> lines,
        bool allowManualLines,
        CancellationToken cancellationToken)
    {
        if (invoice.PurchaseOrderId is not { } orderId)
        {
            // An invoice with no order behind it bills whatever it likes — nothing to check against.
            return [];
        }

        var manual = lines.Where(l => l.PurchaseOrderLineId is null).ToList();
        if (manual.Count > 0 && !allowManualLines)
        {
            throw new BusinessRuleException(
                "PUR-INVOICE-MANUAL-LINE-NOT-ALLOWED",
                "إعدادات دورة المشتريات ماتسمحش بسطور مش من أمر الشراء — كل سطر لازم يتحمّل من الأمر.");
        }

        var orderLines = await LoadOrderLinesAsync(db, lines.Where(l => l.PurchaseOrderLineId is not null).Select(l => l.PurchaseOrderLineId!.Value), cancellationToken);

        var plan = new Dictionary<long, decimal>();
        foreach (var line in lines.Where(l => l.PurchaseOrderLineId is not null))
        {
            var orderLineId = line.PurchaseOrderLineId!.Value;
            if (!orderLines.TryGetValue(orderLineId, out var orderLine) || orderLine.PurchaseOrderId != orderId)
            {
                throw new BusinessRuleException(
                    "PUR-INVOICE-LINE-NOT-IN-ORDER", "فيه سطر مربوط ببند مش من أمر الشراء المختار.");
            }

            if (orderLine.ItemId != line.ItemId)
            {
                throw new BusinessRuleException(
                    "PUR-INVOICE-LINE-ITEM-MISMATCH", "الصنف في الفاتورة مختلف عن صنف بند أمر الشراء المربوط بيه.");
            }

            plan[orderLineId] = plan.GetValueOrDefault(orderLineId) + InOrderLineUnit(line.BaseQuantity, orderLine);
        }

        foreach (var (orderLineId, requested) in plan)
        {
            var orderLine = orderLines[orderLineId];
            var remaining = orderLine.Quantity - orderLine.InvoicedQuantity;
            if (requested > remaining)
            {
                throw new BusinessRuleException(
                    "PUR-INVOICE-QTY-EXCEEDS-ORDER",
                    $"الكمية المفوترة ({requested:N4}) أكبر من المتبقي في بند أمر الشراء ({Math.Max(0m, remaining):N4}).");
            }
        }

        return plan;
    }

    /// <summary>Adds (or, with a negative sign, gives back) the planned quantities and re-reads the order's status.</summary>
    public static async Task ApplyAsync(
        IApplicationDbContext db, long orderId, IReadOnlyDictionary<long, decimal> plan, int sign, CancellationToken cancellationToken)
    {
        if (plan.Count > 0)
        {
            var orderLines = await LoadOrderLinesAsync(db, plan.Keys, cancellationToken);
            foreach (var (orderLineId, quantity) in plan)
            {
                if (orderLines.TryGetValue(orderLineId, out var orderLine))
                {
                    orderLine.InvoicedQuantity = Math.Max(0m, orderLine.InvoicedQuantity + (sign * quantity));
                }
            }
        }

        await RefreshOrderStatusAsync(db, orderId, cancellationToken);
    }

    /// <summary>What an existing invoice has already put on its order — used to take it back before re-applying.</summary>
    public static Dictionary<long, decimal> PlanOf(IEnumerable<PurchaseInvoiceLine> lines, IReadOnlyDictionary<long, PurchaseOrderLine> orderLines)
    {
        var plan = new Dictionary<long, decimal>();
        foreach (var line in lines.Where(l => l.PurchaseOrderLineId is not null))
        {
            var orderLineId = line.PurchaseOrderLineId!.Value;
            if (orderLines.TryGetValue(orderLineId, out var orderLine))
            {
                plan[orderLineId] = plan.GetValueOrDefault(orderLineId) + InOrderLineUnit(line.BaseQuantity, orderLine);
            }
        }

        return plan;
    }

    /// <summary>
    /// Sets the order to Invoiced / PartiallyInvoiced / back to what it was, from its lines. A
    /// Cancelled, Rejected, Closed or Archived order is left alone — its state is a decision, not a total.
    /// </summary>
    public static async Task RefreshOrderStatusAsync(IApplicationDbContext db, long orderId, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null || order.Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Rejected
                or PurchaseOrderStatus.Closed or PurchaseOrderStatus.Archived)
        {
            return;
        }

        var invoicedAnything = order.Lines.Any(l => l.InvoicedQuantity > 0);
        var invoicedEverything = order.Lines.Count > 0 && order.Lines.All(l => l.InvoicedQuantity >= l.Quantity);

        if (invoicedEverything)
        {
            order.Status = PurchaseOrderStatus.Invoiced;
            return;
        }

        if (invoicedAnything)
        {
            order.Status = PurchaseOrderStatus.PartiallyInvoiced;
            return;
        }

        // Nothing billed any more (the last invoice was cancelled): fall back to what receiving says.
        var receivedAnything = order.Lines.Any(l => l.ReceivedQuantity > 0);
        var receivedEverything = order.Lines.Count > 0 && order.Lines.All(l => l.ReceivedQuantity >= l.Quantity);
        order.Status = receivedEverything
            ? PurchaseOrderStatus.FullyReceived
            : receivedAnything
                ? PurchaseOrderStatus.PartiallyReceived
                : PurchaseOrderStatus.Confirmed;
    }
}
