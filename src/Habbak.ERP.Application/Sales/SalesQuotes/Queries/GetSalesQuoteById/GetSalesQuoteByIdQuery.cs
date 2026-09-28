using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.SalesQuotes.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesQuotes.Queries.GetSalesQuoteById;

public sealed record GetSalesQuoteByIdQuery(long Id) : IRequest<SalesQuoteDetailDto>;

public sealed class GetSalesQuoteByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSalesQuoteByIdQuery, SalesQuoteDetailDto>
{
    public async Task<SalesQuoteDetailDto> Handle(GetSalesQuoteByIdQuery request, CancellationToken cancellationToken)
    {
        var quote = await db.SalesQuotes
            .AsNoTracking()
            .Include(q => q.Customer)
            .Include(q => q.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesQuote), request.Id);

        return new SalesQuoteDetailDto
        {
            Id = quote.Id,
            QuoteNumber = quote.QuoteNumber,
            QuoteDate = quote.QuoteDate,
            ValidUntil = quote.ValidUntil,
            BranchId = quote.BranchId,
            CustomerId = quote.CustomerId,
            CustomerNameAr = quote.Customer!.NameAr,
            Subtotal = quote.Subtotal,
            Status = quote.Status.ToString(),
            RowVersion = Convert.ToBase64String(quote.RowVersion),
            Lines = quote.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new SalesQuoteLineDto
                {
                    Id = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    DiscountAmount = l.DiscountAmount,
                    LineTotal = l.LineTotal
                })
                .ToList()
        };
    }
}
