using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesQuotes.Commands.RejectSalesQuote;

/// <summary>Sent → Rejected — terminal (قاعدة 22): a rejected quote is never edited and resent, a
/// fresh quote is required.</summary>
public sealed record RejectSalesQuoteCommand(long Id) : IRequest;

public sealed class RejectSalesQuoteCommandHandler(IApplicationDbContext db) : IRequestHandler<RejectSalesQuoteCommand>
{
    public async Task Handle(RejectSalesQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await db.SalesQuotes.FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesQuote), request.Id);

        if (quote.Status != SalesQuoteStatus.Sent)
        {
            throw new BusinessRuleException("SALES-QUOTE-NOT-SENT", "لا يمكن رفض عرض السعر إلا وهو في حالة مُرسَل.");
        }

        quote.Status = SalesQuoteStatus.Rejected;

        await db.SaveChangesAsync(cancellationToken);
    }
}
