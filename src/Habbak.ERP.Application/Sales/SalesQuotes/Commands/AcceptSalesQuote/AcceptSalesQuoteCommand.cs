using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesQuotes.Commands.AcceptSalesQuote;

/// <summary>Sent → Accepted (04-Module-Sales.md, section 4.1) — makes the quote eligible for
/// conversion into a sales order (قاعدة 15), subject to it not being expired at that point
/// (قاعدة 14, checked by CreateSalesOrderCommand, not here).</summary>
public sealed record AcceptSalesQuoteCommand(long Id) : IRequest;

public sealed class AcceptSalesQuoteCommandHandler(IApplicationDbContext db) : IRequestHandler<AcceptSalesQuoteCommand>
{
    public async Task Handle(AcceptSalesQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await db.SalesQuotes.FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesQuote), request.Id);

        if (quote.Status != SalesQuoteStatus.Sent)
        {
            throw new BusinessRuleException("SALES-QUOTE-NOT-SENT", "لا يمكن قبول عرض السعر إلا وهو في حالة مُرسَل.");
        }

        quote.Status = SalesQuoteStatus.Accepted;

        await db.SaveChangesAsync(cancellationToken);
    }
}
