using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseRequests.Queries.GetOpenPurchaseRequests;

/// <summary>
/// The requests that still have something left to convert to a purchase order (Remarks7) — what the
/// purchase order screen offers as soon as its create form opens.
///
/// "Still open" means at least one line whose OrderedQuantity is below its Quantity, on a request the
/// company actually approved (Approved or PartiallyConverted) and that was not rejected, cancelled or
/// archived. A fully converted request drops off the list by itself. Unlike orders-by-supplier
/// (Remarks6), a request carries no SupplierId, so there is no supplier filter here.
/// </summary>
public sealed record OpenPurchaseRequestDto
{
    public required long Id { get; init; }
    public required string RequestNumber { get; init; }
    public required DateOnly RequestDate { get; init; }
    public required string Status { get; init; }
    public required int RemainingLineCount { get; init; }

    /// <summary>How much of the request is already converted, 0-100 — for the picker's label.</summary>
    public required decimal CompletionPercentage { get; init; }
}

public sealed record GetOpenPurchaseRequestsQuery : IRequest<IReadOnlyList<OpenPurchaseRequestDto>>;

public sealed class GetOpenPurchaseRequestsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetOpenPurchaseRequestsQuery, IReadOnlyList<OpenPurchaseRequestDto>>
{
    private static readonly PurchaseRequestStatus[] Convertible =
    [
        PurchaseRequestStatus.Approved,
        PurchaseRequestStatus.PartiallyConverted
    ];

    public async Task<IReadOnlyList<OpenPurchaseRequestDto>> Handle(
        GetOpenPurchaseRequestsQuery request, CancellationToken cancellationToken)
    {
        var requests = await db.PurchaseRequests.AsNoTracking()
            .Where(r => Convertible.Contains(r.Status))
            .Select(r => new
            {
                r.Id,
                r.RequestNumber,
                r.RequestDate,
                r.Status,
                Lines = r.Lines.Select(l => new { l.Quantity, l.OrderedQuantity }).ToList()
            })
            .OrderByDescending(r => r.RequestDate).ThenByDescending(r => r.Id)
            .ToListAsync(cancellationToken);

        return requests
            .Select(r =>
            {
                var remaining = r.Lines.Count(l => l.OrderedQuantity < l.Quantity);
                var requested = r.Lines.Sum(l => l.Quantity);
                var ordered = r.Lines.Sum(l => Math.Min(l.OrderedQuantity, l.Quantity));
                return new OpenPurchaseRequestDto
                {
                    Id = r.Id,
                    RequestNumber = r.RequestNumber,
                    RequestDate = r.RequestDate,
                    Status = r.Status.ToString(),
                    RemainingLineCount = remaining,
                    CompletionPercentage = requested == 0 ? 0m : Math.Round(ordered / requested * 100m, 2)
                };
            })
            .Where(r => r.RemainingLineCount > 0)
            .ToList();
    }
}
