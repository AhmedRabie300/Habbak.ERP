using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders;

/// <summary>
/// Keeps a purchase request's "how much of this has been ordered" in step with its orders (Remarks7).
///
/// Every order line that came off a request line carries <see cref="PurchaseOrderLine.PurchaseRequestLineId"/>;
/// saving the order adds its quantity to that request line's <see cref="PurchaseRequestLine.OrderedQuantity"/>,
/// editing it takes the old quantities back off first, and cancelling/rejecting it returns them all. The
/// request's status follows: nothing ordered leaves it alone, some of it ordered makes it
/// PartiallyConverted, all of it ordered makes it Converted.
///
/// Quantities are compared in the **request line's own unit**: an order line in a different unit is
/// converted through the two lines' unit factors, so ordering "2 bags" against a request in grams counts
/// as the 2 000 g it is.
/// </summary>
internal static class PurchaseOrderRequestLinking
{
    /// <summary>Loads the request lines an order's lines point at, locked to the same context.</summary>
    public static async Task<Dictionary<long, PurchaseRequestLine>> LoadRequestLinesAsync(
        IApplicationDbContext db, IEnumerable<long> requestLineIds, CancellationToken cancellationToken)
    {
        var ids = requestLineIds.Distinct().ToList();
        return ids.Count == 0
            ? []
            : await db.PurchaseRequestLines.Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);
    }

    /// <summary>The order line's quantity expressed in its request line's unit.</summary>
    public static decimal InRequestLineUnit(decimal baseQuantity, PurchaseRequestLine requestLine) =>
        requestLine.UnitFactor == 0 ? baseQuantity : Math.Round(baseQuantity / requestLine.UnitFactor, 4);

    /// <summary>
    /// Checks every line of an order against the request it converts, then returns what to add to each
    /// request line. Throws before anything is written, so a refused order changes nothing.
    /// </summary>
    public static async Task<Dictionary<long, decimal>> PlanAsync(
        IApplicationDbContext db,
        PurchaseOrder order,
        IReadOnlyCollection<PurchaseOrderLine> lines,
        bool allowManualLines,
        CancellationToken cancellationToken)
    {
        if (order.PurchaseRequestId is not { } requestId)
        {
            // An order with no request behind it orders whatever it likes — nothing to check against.
            return [];
        }

        var manual = lines.Where(l => l.PurchaseRequestLineId is null).ToList();
        if (manual.Count > 0 && !allowManualLines)
        {
            throw new BusinessRuleException(
                "PUR-ORDER-MANUAL-LINE-NOT-ALLOWED",
                "إعدادات دورة المشتريات ماتسمحش بسطور مش من طلب الشراء — كل سطر لازم يتحمّل من الطلب.");
        }

        var requestLines = await LoadRequestLinesAsync(db, lines.Where(l => l.PurchaseRequestLineId is not null).Select(l => l.PurchaseRequestLineId!.Value), cancellationToken);

        var plan = new Dictionary<long, decimal>();
        foreach (var line in lines.Where(l => l.PurchaseRequestLineId is not null))
        {
            var requestLineId = line.PurchaseRequestLineId!.Value;
            if (!requestLines.TryGetValue(requestLineId, out var requestLine) || requestLine.PurchaseRequestId != requestId)
            {
                throw new BusinessRuleException(
                    "PUR-ORDER-LINE-NOT-IN-REQUEST", "فيه سطر مربوط ببند مش من طلب الشراء المختار.");
            }

            if (requestLine.ItemId != line.ItemId)
            {
                throw new BusinessRuleException(
                    "PUR-ORDER-LINE-ITEM-MISMATCH", "الصنف في أمر الشراء مختلف عن صنف بند طلب الشراء المربوط بيه.");
            }

            plan[requestLineId] = plan.GetValueOrDefault(requestLineId) + InRequestLineUnit(line.BaseQuantity, requestLine);
        }

        foreach (var (requestLineId, requested) in plan)
        {
            var requestLine = requestLines[requestLineId];
            var remaining = requestLine.Quantity - requestLine.OrderedQuantity;
            if (requested > remaining)
            {
                throw new BusinessRuleException(
                    "PUR-ORDER-QTY-EXCEEDS-REQUEST",
                    $"الكمية المطلوب شراؤها ({requested:N4}) أكبر من المتبقي في بند طلب الشراء ({Math.Max(0m, remaining):N4}).");
            }
        }

        return plan;
    }

    /// <summary>Adds (or, with a negative sign, gives back) the planned quantities and re-reads the request's status.</summary>
    public static async Task ApplyAsync(
        IApplicationDbContext db, long requestId, IReadOnlyDictionary<long, decimal> plan, int sign, CancellationToken cancellationToken)
    {
        if (plan.Count > 0)
        {
            var requestLines = await LoadRequestLinesAsync(db, plan.Keys, cancellationToken);
            foreach (var (requestLineId, quantity) in plan)
            {
                if (requestLines.TryGetValue(requestLineId, out var requestLine))
                {
                    requestLine.OrderedQuantity = Math.Max(0m, requestLine.OrderedQuantity + (sign * quantity));
                }
            }
        }

        await RefreshRequestStatusAsync(db, requestId, cancellationToken);
    }

    /// <summary>What an existing order has already put on its request — used to take it back before re-applying.</summary>
    public static Dictionary<long, decimal> PlanOf(IEnumerable<PurchaseOrderLine> lines, IReadOnlyDictionary<long, PurchaseRequestLine> requestLines)
    {
        var plan = new Dictionary<long, decimal>();
        foreach (var line in lines.Where(l => l.PurchaseRequestLineId is not null))
        {
            var requestLineId = line.PurchaseRequestLineId!.Value;
            if (requestLines.TryGetValue(requestLineId, out var requestLine))
            {
                plan[requestLineId] = plan.GetValueOrDefault(requestLineId) + InRequestLineUnit(line.BaseQuantity, requestLine);
            }
        }

        return plan;
    }

    /// <summary>
    /// Sets the request to Converted / PartiallyConverted / back to Approved, from its lines. A
    /// Rejected, Cancelled or Archived request is left alone — its state is a decision, not a total.
    /// </summary>
    public static async Task RefreshRequestStatusAsync(IApplicationDbContext db, long requestId, CancellationToken cancellationToken)
    {
        var request = await db.PurchaseRequests.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null || request.Status is PurchaseRequestStatus.Rejected or PurchaseRequestStatus.Cancelled
                or PurchaseRequestStatus.Archived)
        {
            return;
        }

        var orderedAnything = request.Lines.Any(l => l.OrderedQuantity > 0);
        var orderedEverything = request.Lines.Count > 0 && request.Lines.All(l => l.OrderedQuantity >= l.Quantity);

        if (orderedEverything)
        {
            request.Status = PurchaseRequestStatus.Converted;
            return;
        }

        if (orderedAnything)
        {
            request.Status = PurchaseRequestStatus.PartiallyConverted;
            return;
        }

        // Nothing ordered any more (the last order was cancelled/rejected): back to Approved.
        request.Status = PurchaseRequestStatus.Approved;
    }
}
