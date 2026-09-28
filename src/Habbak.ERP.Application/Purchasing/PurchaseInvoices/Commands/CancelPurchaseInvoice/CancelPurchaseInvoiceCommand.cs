using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseInvoices.Commands.CancelPurchaseInvoice;

/// <summary>
/// Draft/PendingApproval/Posted → Cancelled.
///
/// A posted invoice can be cancelled (decided 2026-09-18, replacing the spec's rule 19 "no
/// cancelling after posting"): its journal entry is undone by a reversing entry, posted at once,
/// and the original stays on the books next to it. It is refused once anything else rests on the
/// invoice, because the reversal would only undo the invoice's own entry and leave those standing:
///   - a payment (PartiallyPaid/Paid) — reverse the payment vouchers first;
///   - stock received against it (a posted goods receipt) — that goes back through a purchase return;
///   - a purchase return against it.
/// A draft receipt the invoice created automatically goes down with it, since it only restated
/// the invoice and nothing was received on it.
///
/// SupplierPriceHistory rows stay: the price log records what was quoted, not what stood.
/// </summary>
public sealed record CancelPurchaseInvoiceCommand(long Id, Guid? IdempotencyKey = null) : IRequest, IIdempotentRequest;

public sealed class CancelPurchaseInvoiceCommandHandler(IApplicationDbContext db, IPostingTemplateEngine postingEngine)
    : IRequestHandler<CancelPurchaseInvoiceCommand>
{
    public async Task Handle(CancelPurchaseInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.PurchaseInvoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseInvoice), request.Id);

        if (invoice.Status is PurchaseInvoiceStatus.PartiallyPaid or PurchaseInvoiceStatus.Paid)
        {
            throw new BusinessRuleException(
                "PUR-INVOICE-HAS-PAYMENTS", "الفاتورة عليها سداد — اعكس سندات السداد الأول وبعدين ألغِها.");
        }

        if (invoice.Status is not (PurchaseInvoiceStatus.Draft or PurchaseInvoiceStatus.PendingApproval or PurchaseInvoiceStatus.Posted))
        {
            throw new BusinessRuleException("PUR-INVOICE-NOT-CANCELLABLE", "لا يمكن إلغاء فاتورة الشراء في حالتها الحالية.");
        }

        if (invoice.Status == PurchaseInvoiceStatus.Posted)
        {
            await EnsureNothingRestsOnItAsync(invoice, cancellationToken);
            await CancelDraftReceiptsAsync(invoice.Id, cancellationToken);

            if (invoice.JournalEntryId is { } entryId)
            {
                await postingEngine.ReverseAsync(
                    entryId, DateOnly.FromDateTime(DateTime.UtcNow), $"إلغاء فاتورة الشراء {invoice.InvoiceNumber}", cancellationToken);
            }
        }

        // Remarks6: a cancelled invoice bills nothing, so its quantities go back to the order.
        if (invoice.PurchaseOrderId is { } orderId)
        {
            var orderLines = await PurchaseOrderInvoicing.LoadOrderLinesAsync(
                db, invoice.Lines.Where(l => l.PurchaseOrderLineId is not null).Select(l => l.PurchaseOrderLineId!.Value), cancellationToken);
            await PurchaseOrderInvoicing.ApplyAsync(db, orderId, PurchaseOrderInvoicing.PlanOf(invoice.Lines, orderLines), -1, cancellationToken);
        }

        invoice.Status = PurchaseInvoiceStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNothingRestsOnItAsync(PurchaseInvoice invoice, CancellationToken cancellationToken)
    {
        var received = invoice.Lines.Any(l => l.ReceivedQuantity > 0)
            || await db.GoodsReceipts.AnyAsync(r =>
                r.Status == GoodsReceiptStatus.Posted
                && (r.PurchaseInvoiceId == invoice.Id || r.Id == invoice.GoodsReceiptId),
                cancellationToken);
        if (received)
        {
            throw new BusinessRuleException(
                "PUR-INVOICE-HAS-RECEIPT",
                "البضاعة دي اتستلمت في المخزن على إذن إضافة مرحّل — إلغاء الفاتورة مش هيطلّعها. استخدم مرتجع مشتريات.");
        }

        var returned = await db.PurchaseReturns.AnyAsync(r =>
            r.PurchaseInvoiceId == invoice.Id && r.Status != PurchaseReturnStatus.Cancelled, cancellationToken);
        if (returned)
        {
            throw new BusinessRuleException(
                "PUR-INVOICE-HAS-RETURN", "فيه مرتجع مشتريات على الفاتورة دي — ألغِ المرتجع الأول.");
        }
    }

    private async Task CancelDraftReceiptsAsync(long invoiceId, CancellationToken cancellationToken)
    {
        var drafts = await db.GoodsReceipts
            .Where(r => r.PurchaseInvoiceId == invoiceId && r.Status == GoodsReceiptStatus.Draft)
            .ToListAsync(cancellationToken);

        foreach (var receipt in drafts)
        {
            receipt.Status = GoodsReceiptStatus.Cancelled;
        }
    }
}
