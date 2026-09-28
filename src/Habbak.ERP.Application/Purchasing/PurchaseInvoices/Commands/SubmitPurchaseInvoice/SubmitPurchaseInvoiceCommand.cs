using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseInvoices.Commands.SubmitPurchaseInvoice;

/// <summary>Draft → PendingApproval (section 6.4) — same manual-approval-gate pattern as
/// PurchaseRequest's Submit; PostPurchaseInvoiceCommand later checks
/// PurchaseCycleSettings.RequiresApprovalForInvoice to decide whether this step was mandatory.</summary>
public sealed record SubmitPurchaseInvoiceCommand(long Id) : IRequest;

public sealed class SubmitPurchaseInvoiceCommandHandler(IApplicationDbContext db) : IRequestHandler<SubmitPurchaseInvoiceCommand>
{
    public async Task Handle(SubmitPurchaseInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.PurchaseInvoices.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseInvoice), request.Id);

        if (invoice.Status != PurchaseInvoiceStatus.Draft)
        {
            throw new BusinessRuleException("PUR-INVOICE-NOT-DRAFT", "لا يمكن تقديم فاتورة الشراء للاعتماد إلا وهي في حالة مسودة.");
        }

        invoice.Status = PurchaseInvoiceStatus.PendingApproval;

        await db.SaveChangesAsync(cancellationToken);
    }
}
