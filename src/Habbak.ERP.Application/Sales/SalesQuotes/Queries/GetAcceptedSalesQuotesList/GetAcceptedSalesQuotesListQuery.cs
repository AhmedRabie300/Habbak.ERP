using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.SalesQuotes.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesQuotes.Queries.GetAcceptedSalesQuotesList;

/// <summary>Feeds screen #6's "convert to sales order" picker — only Accepted quotes not yet past
/// ValidUntil (قاعدة 14) are offered; CreateSalesOrderCommand re-checks both conditions itself
/// regardless of what this list shows.</summary>
public sealed record GetAcceptedSalesQuotesListQuery : IRequest<IReadOnlyList<AcceptedSalesQuoteListItemDto>>;

public sealed class GetAcceptedSalesQuotesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAcceptedSalesQuotesListQuery, IReadOnlyList<AcceptedSalesQuoteListItemDto>>
{
    public async Task<IReadOnlyList<AcceptedSalesQuoteListItemDto>> Handle(
        GetAcceptedSalesQuotesListQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await db.SalesQuotes
            .AsNoTracking()
            .Where(q => q.Status == SalesQuoteStatus.Accepted && q.ValidUntil >= today)
            .OrderByDescending(q => q.QuoteDate)
            .Select(q => new AcceptedSalesQuoteListItemDto
            {
                Id = q.Id,
                QuoteNumber = q.QuoteNumber,
                CustomerId = q.CustomerId,
                CustomerNameAr = q.Customer!.NameAr,
                ValidUntil = q.ValidUntil,
                Subtotal = q.Subtotal
            })
            .ToListAsync(cancellationToken);
    }
}
