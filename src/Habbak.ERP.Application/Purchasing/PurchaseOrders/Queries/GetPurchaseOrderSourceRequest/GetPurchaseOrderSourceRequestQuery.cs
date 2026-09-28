using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderSourceRequest;

/// <summary>
/// What the purchase request behind an order actually asked for (Remarks4, item 4). The order screen
/// showed the request's number and nothing else, so a buyer had to open the request in another tab to
/// see whether what is being ordered is what was asked for.
///
/// Per item: what the request asked for, what this order covers, and what every other order raised
/// from the same request already took — so "still not ordered" is a number the buyer can trust.
/// </summary>
public sealed record PurchaseRequestLineCoverageDto
{
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal RequestedQuantity { get; init; }
    public required string RequestedUnitCode { get; init; }

    /// <summary>In base units, so quantities entered in different units still compare.</summary>
    public required decimal RequestedBaseQuantity { get; init; }
    public required decimal OrderedOnThisOrderBaseQuantity { get; init; }
    public required decimal OrderedElsewhereBaseQuantity { get; init; }
    public required decimal RemainingBaseQuantity { get; init; }
    public string? Notes { get; init; }
}

public sealed record PurchaseOrderSourceRequestDto
{
    public required long PurchaseRequestId { get; init; }
    public required string RequestNumber { get; init; }
    public required DateOnly RequestDate { get; init; }
    public required string Priority { get; init; }
    public required string Status { get; init; }
    public required long RequestedByUserId { get; init; }
    public string? RequestedByName { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<PurchaseRequestLineCoverageDto> Lines { get; init; }
}

/// <summary>Null when the order was raised without a request.</summary>
public sealed record GetPurchaseOrderSourceRequestQuery(long OrderId) : IRequest<PurchaseOrderSourceRequestDto?>;

public sealed class GetPurchaseOrderSourceRequestQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseOrderSourceRequestQuery, PurchaseOrderSourceRequestDto?>
{
    public async Task<PurchaseOrderSourceRequestDto?> Handle(GetPurchaseOrderSourceRequestQuery request, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.AsNoTracking()
                        .Where(o => o.Id == request.OrderId)
                        .Select(o => new { o.Id, o.PurchaseRequestId })
                        .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new NotFoundException(nameof(PurchaseOrder), request.OrderId);

        if (order.PurchaseRequestId is not { } requestId)
        {
            return null;
        }

        var sourceRequest = await db.PurchaseRequests.AsNoTracking()
                                .Include(r => r.Lines).ThenInclude(l => l.Item)
                                .Include(r => r.Lines).ThenInclude(l => l.Unit)
                                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
                            ?? throw new NotFoundException(nameof(PurchaseRequest), requestId);

        // Remarks7: PurchaseRequestLine.OrderedQuantity is the running total across every order raised
        // from this request — PurchaseOrderRequestLinking keeps it in step on every create/edit/cancel/
        // reject — so there is no need to join every other order any more, only this order's own lines.
        var onThisOrder = await db.PurchaseOrderLines.AsNoTracking()
            .Where(l => l.PurchaseOrderId == order.Id && l.PurchaseRequestLineId != null)
            .Select(l => new { RequestLineId = l.PurchaseRequestLineId!.Value, l.BaseQuantity })
            .ToListAsync(cancellationToken);

        var requestedBy = await db.Users.AsNoTracking()
            .Where(u => u.Id == sourceRequest.RequestedByUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var lines = sourceRequest.Lines
            .OrderBy(l => l.Id)
            .Select(l =>
            {
                var onThis = onThisOrder.Where(o => o.RequestLineId == l.Id).Sum(o => o.BaseQuantity);
                // OrderedQuantity is kept in the request line's own unit — back to base units to match
                // the DTO's existing shape, the same conversion PurchaseOrderRequestLinking used going in.
                var orderedTotalBaseQuantity = l.OrderedQuantity * l.UnitFactor;
                var elsewhere = Math.Max(0m, orderedTotalBaseQuantity - onThis);
                return new PurchaseRequestLineCoverageDto
                {
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    RequestedQuantity = l.Quantity,
                    RequestedUnitCode = l.Unit!.Code,
                    RequestedBaseQuantity = l.BaseQuantity,
                    OrderedOnThisOrderBaseQuantity = onThis,
                    OrderedElsewhereBaseQuantity = elsewhere,
                    RemainingBaseQuantity = Math.Max(0m, l.BaseQuantity - orderedTotalBaseQuantity),
                    Notes = l.Notes
                };
            })
            .ToList();

        return new PurchaseOrderSourceRequestDto
        {
            PurchaseRequestId = sourceRequest.Id,
            RequestNumber = sourceRequest.RequestNumber,
            RequestDate = sourceRequest.RequestDate,
            Priority = sourceRequest.Priority.ToString(),
            Status = sourceRequest.Status.ToString(),
            RequestedByUserId = sourceRequest.RequestedByUserId,
            RequestedByName = requestedBy,
            Reason = sourceRequest.Reason,
            Notes = sourceRequest.Notes,
            Lines = lines
        };
    }
}
