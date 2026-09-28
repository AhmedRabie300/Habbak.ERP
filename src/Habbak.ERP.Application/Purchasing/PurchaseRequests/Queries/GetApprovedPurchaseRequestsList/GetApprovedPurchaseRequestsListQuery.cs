using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseRequests.Queries.GetApprovedPurchaseRequestsList;

public sealed record ApprovedPurchaseRequestDto(long Id, string RequestNumber, DateOnly RequestDate);

/// <summary>Feeds the "convert to purchase order" picker on screen #4's create form — only
/// Approved requests are convertible (rule: creating a PurchaseOrder from a request requires it be
/// Approved).</summary>
public sealed record GetApprovedPurchaseRequestsListQuery : IRequest<IReadOnlyList<ApprovedPurchaseRequestDto>>;

public sealed class GetApprovedPurchaseRequestsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetApprovedPurchaseRequestsListQuery, IReadOnlyList<ApprovedPurchaseRequestDto>>
{
    public async Task<IReadOnlyList<ApprovedPurchaseRequestDto>> Handle(
        GetApprovedPurchaseRequestsListQuery request, CancellationToken cancellationToken)
    {
        return await db.PurchaseRequests
            .AsNoTracking()
            .Where(r => r.Status == PurchaseRequestStatus.Approved)
            .OrderByDescending(r => r.RequestDate)
            .Select(r => new ApprovedPurchaseRequestDto(r.Id, r.RequestNumber, r.RequestDate))
            .ToListAsync(cancellationToken);
    }
}
