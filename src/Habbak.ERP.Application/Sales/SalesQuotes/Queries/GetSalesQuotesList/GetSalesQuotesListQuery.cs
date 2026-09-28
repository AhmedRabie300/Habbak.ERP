using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Sales.SalesQuotes.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesQuotes.Queries.GetSalesQuotesList;

public sealed class GetSalesQuotesListQuery : ListQuery, IRequest<PagedResult<SalesQuoteListItemDto>>;

public sealed class GetSalesQuotesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSalesQuotesListQuery, PagedResult<SalesQuoteListItemDto>>
{
    public async Task<PagedResult<SalesQuoteListItemDto>> Handle(GetSalesQuotesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.SalesQuotes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<SalesQuoteStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            query = query.Where(q =>
                q.QuoteNumber.Contains(term) ||
                q.Customer!.NameAr.Contains(term) ||
                matchingStatuses.Contains(q.Status));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "quoteDate" => descending ? query.OrderByDescending(q => q.QuoteDate) : query.OrderBy(q => q.QuoteDate),
            "status" => descending ? query.OrderByDescending(q => q.Status) : query.OrderBy(q => q.Status),
            _ => descending ? query.OrderByDescending(q => q.QuoteNumber) : query.OrderBy(q => q.QuoteNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new SalesQuoteListItemDto
            {
                Id = q.Id,
                QuoteNumber = q.QuoteNumber,
                QuoteDate = q.QuoteDate,
                ValidUntil = q.ValidUntil,
                BranchId = q.BranchId,
                CustomerId = q.CustomerId,
                CustomerNameAr = q.Customer!.NameAr,
                Subtotal = q.Subtotal,
                Status = q.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SalesQuoteListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
