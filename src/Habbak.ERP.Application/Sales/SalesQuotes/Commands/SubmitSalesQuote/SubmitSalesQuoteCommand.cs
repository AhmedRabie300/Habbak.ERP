using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesQuotes.Commands.SubmitSalesQuote;

/// <summary>Draft → Sent (04-Module-Sales.md, section 4.1).</summary>
public sealed record SubmitSalesQuoteCommand(long Id) : IRequest;

public sealed class SubmitSalesQuoteCommandHandler(IApplicationDbContext db) : IRequestHandler<SubmitSalesQuoteCommand>
{
    public async Task Handle(SubmitSalesQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await db.SalesQuotes.FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesQuote), request.Id);

        if (quote.Status != SalesQuoteStatus.Draft)
        {
            throw new BusinessRuleException("SALES-QUOTE-NOT-DRAFT", "لا يمكن إرسال عرض السعر إلا وهو في حالة مسودة.");
        }

        quote.Status = SalesQuoteStatus.Sent;

        await db.SaveChangesAsync(cancellationToken);
    }
}
