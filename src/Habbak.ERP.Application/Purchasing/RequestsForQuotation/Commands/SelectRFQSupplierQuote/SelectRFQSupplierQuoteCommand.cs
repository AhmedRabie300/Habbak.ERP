using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.SelectRFQSupplierQuote;

/// <summary>
/// Awards one RFQLine's winning quote — rule 18: an expired quote (ValidUntil in the past) can
/// never be selected. Unselects any previously-selected quote for the SAME RFQLineId first, since
/// only one supplier can win a given line.
/// </summary>
public sealed record SelectRFQSupplierQuoteCommand(long RFQId, long QuoteId) : IRequest;

public sealed class SelectRFQSupplierQuoteCommandHandler(IApplicationDbContext db) : IRequestHandler<SelectRFQSupplierQuoteCommand>
{
    public async Task Handle(SelectRFQSupplierQuoteCommand request, CancellationToken cancellationToken)
    {
        var rfq = await db.RequestsForQuotation.FirstOrDefaultAsync(r => r.Id == request.RFQId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Purchasing.RequestForQuotation), request.RFQId);

        if (rfq.Status != RFQStatus.UnderReview)
        {
            throw new BusinessRuleException("PUR-RFQ-NOT-UNDER-REVIEW", "لا يمكن اختيار عرض إلا وطلب عروض الأسعار قيد المراجعة.");
        }

        var quote = await db.RFQSupplierQuotes.FirstOrDefaultAsync(q => q.Id == request.QuoteId, cancellationToken)
            ?? throw new NotFoundException(nameof(RFQSupplierQuote), request.QuoteId);

        if (quote.ValidUntil < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new BusinessRuleException("PUR-RFQ-QUOTE-EXPIRED", "لا يمكن اختيار عرض منتهي الصلاحية.");
        }

        var otherQuotesForLine = await db.RFQSupplierQuotes
            .Where(q => q.RFQLineId == quote.RFQLineId && q.Id != quote.Id && q.IsSelected)
            .ToListAsync(cancellationToken);
        foreach (var other in otherQuotesForLine)
        {
            other.IsSelected = false;
        }

        quote.IsSelected = true;

        await db.SaveChangesAsync(cancellationToken);
    }
}
