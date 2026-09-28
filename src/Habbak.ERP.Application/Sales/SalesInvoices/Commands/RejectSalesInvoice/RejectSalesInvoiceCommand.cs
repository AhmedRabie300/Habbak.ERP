using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesInvoices.Commands.RejectSalesInvoice;

/// <summary>Draft → Rejected — terminal (قاعدة 22): a rejected invoice is never edited and
/// reposted, a fresh invoice is required.</summary>
public sealed record RejectSalesInvoiceCommand(long Id) : IRequest;

public sealed class RejectSalesInvoiceCommandHandler(IApplicationDbContext db) : IRequestHandler<RejectSalesInvoiceCommand>
{
    public async Task Handle(RejectSalesInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.SalesInvoices.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesInvoice), request.Id);

        if (invoice.Status != SalesInvoiceStatus.Draft)
        {
            throw new BusinessRuleException("SALES-INVOICE-NOT-DRAFT", "لا يمكن رفض فاتورة المبيعات إلا وهي في حالة مسودة.");
        }

        invoice.Status = SalesInvoiceStatus.Rejected;

        await db.SaveChangesAsync(cancellationToken);
    }
}
