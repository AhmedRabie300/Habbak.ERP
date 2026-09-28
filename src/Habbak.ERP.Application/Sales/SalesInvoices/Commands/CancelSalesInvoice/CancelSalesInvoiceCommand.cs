using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesInvoices.Commands.CancelSalesInvoice;

/// <summary>
/// Draft/Posted → Cancelled. A posted invoice is undone by a reversing entry (decided 2026-09-18 —
/// cancelling any posted document reverses its entry), the same rule as the purchase invoice.
///
/// Refused once anything else rests on the invoice, since the reversal would only undo the
/// invoice's own entry:
///   - money received against it — reverse the receipt vouchers first;
///   - goods despatched on a posted delivery order — those come back through a sales return;
///   - a sales return against it.
/// </summary>
public sealed record CancelSalesInvoiceCommand(long Id, Guid? IdempotencyKey = null) : IRequest, IIdempotentRequest;

public sealed class CancelSalesInvoiceCommandHandler(IApplicationDbContext db, IPostingTemplateEngine postingEngine)
    : IRequestHandler<CancelSalesInvoiceCommand>
{
    public async Task Handle(CancelSalesInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.SalesInvoices.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesInvoice), request.Id);

        if (invoice.Status is not (SalesInvoiceStatus.Draft or SalesInvoiceStatus.Posted))
        {
            throw new BusinessRuleException("SALES-INVOICE-NOT-CANCELLABLE", "لا يمكن إلغاء فاتورة المبيعات في حالتها الحالية.");
        }

        if (invoice.Status == SalesInvoiceStatus.Posted)
        {
            if (invoice.AmountPaid > 0)
            {
                throw new BusinessRuleException(
                    "SALES-INVOICE-HAS-RECEIPTS", "الفاتورة عليها تحصيل — اعكس سندات القبض الأول وبعدين ألغِها.");
            }

            if (await db.DeliveryOrders.AnyAsync(d => d.SourceInvoiceId == invoice.Id && d.Status == DeliveryOrderStatus.Posted, cancellationToken))
            {
                throw new BusinessRuleException(
                    "SALES-INVOICE-HAS-DELIVERY", "البضاعة اتسلّمت بأمر تسليم مرحّل — إلغاء الفاتورة مش هيرجّعها. استخدم مرتجع مبيعات.");
            }

            if (await db.SalesReturns.AnyAsync(r => r.SourceInvoiceId == invoice.Id && r.Status != SalesReturnStatus.Cancelled && r.Status != SalesReturnStatus.Rejected, cancellationToken))
            {
                throw new BusinessRuleException("SALES-INVOICE-HAS-RETURN", "فيه مرتجع مبيعات على الفاتورة دي — ألغِ المرتجع الأول.");
            }

            if (invoice.JournalEntryId is { } entryId)
            {
                await postingEngine.ReverseAsync(
                    entryId, DateOnly.FromDateTime(DateTime.UtcNow), $"إلغاء فاتورة المبيعات {invoice.InvoiceNumber}", cancellationToken);
            }
        }

        invoice.Status = SalesInvoiceStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
    }
}
