using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Inventory.BranchRequests.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.BranchRequests.Queries.GetBranchRequestsList;

public sealed class GetBranchRequestsListQuery : ListQuery, IRequest<PagedResult<BranchRequestListItemDto>>;

public sealed class GetBranchRequestsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetBranchRequestsListQuery, PagedResult<BranchRequestListItemDto>>
{
    public async Task<PagedResult<BranchRequestListItemDto>> Handle(
        GetBranchRequestsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.BranchRequests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<BranchRequestStatus>()
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
            .Select(r => new BranchRequestListItemDto
            {
                Id = r.Id,
                RequestNumber = r.RequestNumber,
                RequestDate = r.RequestDate,
                BranchId = r.BranchId,
                LineCount = r.Lines.Count,
                Status = r.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<BranchRequestListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
