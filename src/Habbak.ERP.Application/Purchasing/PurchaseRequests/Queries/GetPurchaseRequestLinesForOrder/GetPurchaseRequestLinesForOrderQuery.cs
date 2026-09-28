using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseRequests.Queries.GetPurchaseRequestLinesForOrder;

/// <summary>
/// What a request still has left to convert, line by line (Remarks7) — the purchase order screen fills
/// itself from this the moment a request is picked, so nobody retypes quantities and units that are
/// already agreed.
///
/// Only lines with something left are returned. When an order is being edited, its own lines are
/// excluded from "already ordered" (via <paramref name="ExcludeOrderId"/>), so raising a line it
/// already owns is not counted against itself.
///
/// A request carries no price of its own — <see cref="PurchaseRequestLineForOrderDto.UnitPrice"/> is a
/// suggested default: the most recent <see cref="SupplierPriceHistory"/> row for the item across any
/// supplier, falling back to the item's StandardCost, then 0. The buyer edits it before saving either way.
/// </summary>
public sealed record PurchaseRequestLineForOrderDto
{
    public required long PurchaseRequestLineId { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required long UnitId { get; init; }
    public required string UnitCode { get; init; }
    public required decimal UnitFactor { get; init; }
    public required decimal RequestedQuantity { get; init; }
    public required decimal OrderedQuantity { get; init; }
    public required decimal RemainingQuantity { get; init; }
    public required decimal UnitPrice { get; init; }
}

public sealed record GetPurchaseRequestLinesForOrderQuery(long PurchaseRequestId, long? ExcludeOrderId = null)
    : IRequest<IReadOnlyList<PurchaseRequestLineForOrderDto>>;

public sealed class GetPurchaseRequestLinesForOrderQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseRequestLinesForOrderQuery, IReadOnlyList<PurchaseRequestLineForOrderDto>>
{
    public async Task<IReadOnlyList<PurchaseRequestLineForOrderDto>> Handle(
        GetPurchaseRequestLinesForOrderQuery request, CancellationToken cancellationToken)
    {
        var purchaseRequest = await db.PurchaseRequests.AsNoTracking()
                        .Include(r => r.Lines).ThenInclude(l => l.Item)
                        .Include(r => r.Lines).ThenInclude(l => l.Unit)
                        .FirstOrDefaultAsync(r => r.Id == request.PurchaseRequestId, cancellationToken)
                    ?? throw new NotFoundException(nameof(PurchaseRequest), request.PurchaseRequestId);

        if (purchaseRequest.Status is PurchaseRequestStatus.Rejected or PurchaseRequestStatus.Cancelled)
        {
            throw new BusinessRuleException(
                "PUR-ORDER-REQUEST-NOT-CONVERTIBLE", "طلب الشراء ده ملغي أو مرفوض — مينفعش يتحوّل لأمر شراء.");
        }

        // The order being edited gives its own quantities back before we count what is left.
        var ownedByThisOrder = request.ExcludeOrderId is { } orderId
            ? await db.PurchaseOrderLines.AsNoTracking()
                .Where(l => l.PurchaseOrderId == orderId && l.PurchaseRequestLineId != null)
                .Select(l => new { RequestLineId = l.PurchaseRequestLineId!.Value, l.BaseQuantity })
                .ToListAsync(cancellationToken)
            : [];

        var itemIds = purchaseRequest.Lines.Select(l => l.ItemId).Distinct().ToList();
        var latestPrices = await db.SupplierPriceHistories.AsNoTracking()
            .Where(h => itemIds.Contains(h.ItemId))
            .GroupBy(h => h.ItemId)
            .Select(g => new { ItemId = g.Key, UnitPrice = g.OrderByDescending(h => h.EffectiveDate).ThenByDescending(h => h.Id).First().UnitPrice })
            .ToDictionaryAsync(x => x.ItemId, x => x.UnitPrice, cancellationToken);

        return purchaseRequest.Lines
            .OrderBy(l => l.Id)
            .Select(l =>
            {
                var givenBack = ownedByThisOrder
                    .Where(o => o.RequestLineId == l.Id)
                    .Sum(o => l.UnitFactor == 0 ? o.BaseQuantity : Math.Round(o.BaseQuantity / l.UnitFactor, 4));
                var ordered = Math.Max(0m, l.OrderedQuantity - givenBack);
                return new PurchaseRequestLineForOrderDto
                {
                    PurchaseRequestLineId = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    UnitId = l.UnitId,
                    UnitCode = l.Unit!.Code,
                    UnitFactor = l.UnitFactor,
                    RequestedQuantity = l.Quantity,
                    OrderedQuantity = ordered,
                    RemainingQuantity = Math.Max(0m, l.Quantity - ordered),
                    // StandardCost is always per the item's base unit — × UnitFactor converts it to a
                    // per-this-unit price, matching the DTO's own unit.
                    UnitPrice = latestPrices.TryGetValue(l.ItemId, out var historyPrice)
                        ? historyPrice
                        : (l.Item!.StandardCost ?? 0m) * l.UnitFactor
                };
            })
            .Where(l => l.RemainingQuantity > 0)
            .ToList();
    }
}
