using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Vouchers.Commands.ReverseVoucher;

/// <summary>
/// "زر عكس" on a Posted voucher's screen (01-Module-Accounting.md, section 5, screens 5-6).
/// Reverses the voucher's underlying JournalEntry only — the Voucher itself stays Posted as an
/// accurate historical record of "this cash movement was recorded on this date"; the correction
/// lives at the journal level (section 4.2: "يتطلب قيد عكسي زي أي قيد آلي"). There is no
/// "Reversed" status on Voucher itself (its Status enum is Draft/Posted/Rejected/Cancelled).
///
/// Mirrors PostVoucherCommand's supplier-payment side effect in reverse: a reversed Payment
/// voucher against a Supplier with a RelatedInvoiceId un-applies its Amount from that
/// PurchaseInvoice's AmountPaid, dropping its Status back to PartiallyPaid or Posted.
/// </summary>
public sealed record ReverseVoucherCommand(long Id) : IRequest<long>;

public sealed class ReverseVoucherCommandHandler(IApplicationDbContext db, IPostingService postingService)
    : IRequestHandler<ReverseVoucherCommand, long>
{
    public async Task<long> Handle(ReverseVoucherCommand request, CancellationToken cancellationToken)
    {
        var voucher = await db.Vouchers.FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Voucher), request.Id);

        if (voucher.Status != VoucherStatus.Posted || voucher.JournalEntryId is null)
        {
            throw new BusinessRuleException(
                "ACC-VOUCHER-NOT-POSTED", "لا يمكن عكس سند إلا وهو في حالة مرحّل.");
        }

        var result = await postingService.ReverseAsync(voucher.JournalEntryId.Value, cancellationToken);

        // Allocations first (Remarks4, item 7): each invoice gets back exactly what this payment
        // put on it.
        var reversedAllocations = false;
        if (voucher.VoucherType == VoucherType.Payment && voucher.CounterpartyType == CounterpartyType.Supplier)
        {
            var allocations = await db.SupplierPaymentAllocations
                .Include(a => a.PurchaseInvoice)
                .Where(a => a.VoucherId == voucher.Id)
                .ToListAsync(cancellationToken);

            foreach (var allocation in allocations)
            {
                var allocated = allocation.PurchaseInvoice!;
                allocated.AmountPaid -= allocation.Amount;
                allocated.Status = allocated.AmountPaid <= 0 ? PurchaseInvoiceStatus.Posted : PurchaseInvoiceStatus.PartiallyPaid;
                reversedAllocations = true;
            }
        }

        if (!reversedAllocations && voucher.VoucherType == VoucherType.Payment
            && voucher.CounterpartyType == CounterpartyType.Supplier && voucher.RelatedInvoiceId is { } invoiceId)
        {
            var invoice = await db.PurchaseInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
            if (invoice is not null && invoice.Status is PurchaseInvoiceStatus.PartiallyPaid or PurchaseInvoiceStatus.Paid)
            {
                invoice.AmountPaid -= voucher.Amount;
                invoice.Status = invoice.AmountPaid <= 0 ? PurchaseInvoiceStatus.Posted : PurchaseInvoiceStatus.PartiallyPaid;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return result.JournalEntry.Id;
    }
}
