using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseRequests.Queries.GetPurchaseRequestsList;

public sealed class GetPurchaseRequestsListQuery : ListQuery, IRequest<PagedResult<PurchaseRequestListItemDto>>;

public sealed class GetPurchaseRequestsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseRequestsListQuery, PagedResult<PurchaseRequestListItemDto>>
{
    public async Task<PagedResult<PurchaseRequestListItemDto>> Handle(GetPurchaseRequestsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.PurchaseRequests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<PurchaseRequestStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            DateOnly? dateTerm = DateOnly.TryParse(term, out var parsedDate) ? parsedDate : null;

            query = query.Where(r =>
                r.RequestNumber.Contains(term) ||
                matchingStatuses.Contains(r.Status) ||
                (dateTerm != null && r.RequestDate == dateTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "requestDate" => descending ? query.OrderByDescending(r => r.RequestDate) : query.OrderBy(r => r.RequestDate),
            "status" => descending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            _ => descending ? query.OrderByDescending(r => r.RequestNumber) : query.OrderBy(r => r.RequestNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new PurchaseRequestListItemDto
            {
                Id = r.Id,
                RequestNumber = r.RequestNumber,
                RequestDate = r.RequestDate,
                BranchId = r.BranchId,
                Priority = r.Priority.ToString(),
                LineCount = r.Lines.Count,
                Status = r.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseRequestListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
