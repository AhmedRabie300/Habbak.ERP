using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Sales;
using Habbak.ERP.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesInvoices.Commands.PostSalesInvoice;

/// <summary>
/// Draft → Posted (section 4.3). Unlike PurchaseInvoice, there is no PendingApproval gate here:
/// SalesCycleSettings defines no RequiresApprovalForInvoice-equivalent flag, so Draft always posts
/// directly.
///
/// The entries come from the SALES_INVOICE templates when any is active — a template triggered on
/// PaymentType runs only for its kind of invoice. It covers revenue, tax and the customer only:
/// the cost of the goods is posted by the delivery order, the first moment that cost is known
/// (rule 42 — a sales invoice names no warehouse).
///
/// Always debits the customer, cash or credit: a receipt voucher records the money coming in and
/// posts Dr treasury / Cr customer itself (PostVoucherCommand), the mirror of the purchase side.
///
/// Posted is the resting "awaiting payment" state; AmountPaid is written by receipt vouchers.
/// </summary>
public sealed record PostSalesInvoiceCommand(long Id, Guid? IdempotencyKey = null) : IRequest, IIdempotentRequest;

public sealed class PostSalesInvoiceCommandHandler(IApplicationDbContext db, IPostingTemplateEngine postingEngine)
    : IRequestHandler<PostSalesInvoiceCommand>
{
    public async Task Handle(PostSalesInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.SalesInvoices.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesInvoice), request.Id);

        if (invoice.Status != SalesInvoiceStatus.Draft)
        {
            throw new BusinessRuleException("SALES-INVOICE-NOT-DRAFT", "لا يمكن ترحيل فاتورة المبيعات إلا وهي في حالة مسودة.");
        }

        invoice.Status = SalesInvoiceStatus.Posted;

        var companyId = invoice.CompanyId!.Value;
        var discount = invoice.DiscountAmount ?? 0m;
        invoice.JournalEntry = await postingEngine.PostIfConfiguredAsync(new TemplatePostingRequest
        {
            CompanyId = companyId,
            BranchId = invoice.BranchId,
            ScreenCode = PostingScreenCatalog.SalesInvoice,
            SourceModule = SourceModule.Sales,
            SourceDocumentType = SourceDocumentType.Invoice,
            SourceDocumentId = invoice.Id,
            EntryDate = invoice.InvoiceDate,
            Description = $"فاتورة مبيعات {invoice.InvoiceNumber}",
            IdempotencyKey = PostingKeys.For(companyId, "SalesInvoice.Post", invoice.Id),
            Context = PostingContext.Create(new Dictionary<string, object?>
            {
                ["CustomerId"] = invoice.CustomerId,
                ["BranchId"] = invoice.BranchId,
                ["PaymentType"] = invoice.PaymentType.ToString(),
                ["Subtotal"] = invoice.Subtotal,
                ["DiscountAmount"] = discount,
                ["NetAmount"] = invoice.Subtotal - discount,
                ["TaxAmount"] = invoice.TaxAmount,
                ["TotalAmount"] = invoice.TotalAmount
            })
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
