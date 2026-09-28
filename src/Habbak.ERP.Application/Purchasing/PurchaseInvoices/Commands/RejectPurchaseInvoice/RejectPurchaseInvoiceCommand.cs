using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseInvoices.Commands.RejectPurchaseInvoice;

/// <summary>Draft/PendingApproval → Rejected — terminal, same rule 20 pattern as every other
/// approval-gated entity: a rejected invoice is never edited and resubmitted, a fresh attempt needs
/// a new invoice.</summary>
public sealed record RejectPurchaseInvoiceCommand(long Id) : IRequest;

public sealed class RejectPurchaseInvoiceCommandHandler(IApplicationDbContext db) : IRequestHandler<RejectPurchaseInvoiceCommand>
{
    public async Task Handle(RejectPurchaseInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.PurchaseInvoices.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseInvoice), request.Id);

        if (invoice.Status is not (PurchaseInvoiceStatus.Draft or PurchaseInvoiceStatus.PendingApproval))
        {
            throw new BusinessRuleException("PUR-INVOICE-NOT-REJECTABLE", "لا يمكن رفض فاتورة الشراء بعد ترحيلها.");
        }

        invoice.Status = PurchaseInvoiceStatus.Rejected;

        await db.SaveChangesAsync(cancellationToken);
    }
}
