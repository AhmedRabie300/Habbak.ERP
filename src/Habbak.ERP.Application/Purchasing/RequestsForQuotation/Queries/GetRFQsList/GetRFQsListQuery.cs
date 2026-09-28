using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.RequestsForQuotation.Queries.GetRFQsList;

public sealed class GetRFQsListQuery : ListQuery, IRequest<PagedResult<RFQListItemDto>>;

public sealed class GetRFQsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetRFQsListQuery, PagedResult<RFQListItemDto>>
{
    public async Task<PagedResult<RFQListItemDto>> Handle(GetRFQsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.RequestsForQuotation.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<RFQStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            DateOnly? dateTerm = DateOnly.TryParse(term, out var parsedDate) ? parsedDate : null;

            query = query.Where(r =>
                r.RFQNumber.Contains(term) ||
                matchingStatuses.Contains(r.Status) ||
                (dateTerm != null && r.RFQDate == dateTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "rfqDate" => descending ? query.OrderByDescending(r => r.RFQDate) : query.OrderBy(r => r.RFQDate),
            "status" => descending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            _ => descending ? query.OrderByDescending(r => r.RFQNumber) : query.OrderBy(r => r.RFQNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RFQListItemDto
            {
                Id = r.Id,
                RFQNumber = r.RFQNumber,
                RFQDate = r.RFQDate,
                LineCount = r.Lines.Count,
                SupplierCount = r.Suppliers.Count,
                Status = r.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<RFQListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
