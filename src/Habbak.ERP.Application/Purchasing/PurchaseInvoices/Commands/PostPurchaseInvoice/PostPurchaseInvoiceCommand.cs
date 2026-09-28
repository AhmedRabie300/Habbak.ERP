using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Purchasing;
using Habbak.ERP.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseInvoices.Commands.PostPurchaseInvoice;

/// <summary>
/// Draft/PendingApproval → Posted (section 6.4 "مرحّل (ينشئ قيداً محاسبياً)"). The entry comes
/// from the posting template engine when the company has an active purchase-invoice template;
/// without one the invoice posts with no entry, as every invoice did before the engine existed.
///
/// Rule PurchaseCycleSettings.RequiresApprovalForInvoice: when true, Posting is only allowed from
/// PendingApproval (the invoice must have gone through Submit first) — Draft is rejected. When
/// false, either Draft or PendingApproval may be posted directly.
///
/// Posted is used here as the resting "awaiting payment" state; PartiallyPaid/Paid are reached once
/// a supplier payment voucher posts against this invoice (PostVoucherCommand, section 4.9's sibling
/// feature) — PendingPayment/Overdue stay forward-declared and unreachable (the former is redundant
/// with Posted itself, the latter would need a scheduled job this codebase has none of).
///
/// Also records one SupplierPriceHistory row per line (section 4.9) — the append-only price log a
/// future price-trend report reads; never updated or reversed even if the invoice is later cancelled
/// by a return, matching a real paper trail.
/// </summary>
public sealed record PostPurchaseInvoiceCommand(long Id, Guid? IdempotencyKey = null) : IRequest, IIdempotentRequest;

public sealed class PostPurchaseInvoiceCommandHandler(
    IApplicationDbContext db,
    ICurrentCompanyContext currentCompanyContext,
    ICodeGenerator codeGenerator,
    IPostingTemplateEngine postingEngine)
    : IRequestHandler<PostPurchaseInvoiceCommand>
{
    public async Task Handle(PostPurchaseInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.PurchaseInvoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseInvoice), request.Id);

        if (invoice.Status is not (PurchaseInvoiceStatus.Draft or PurchaseInvoiceStatus.PendingApproval))
        {
            throw new BusinessRuleException("PUR-INVOICE-NOT-POSTABLE", "لا يمكن ترحيل فاتورة الشراء في حالتها الحالية.");
        }

        var cycleSettings = await db.PurchaseCycleSettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);
        if (cycleSettings?.RequiresApprovalForInvoice == true && invoice.Status != PurchaseInvoiceStatus.PendingApproval)
        {
            throw new BusinessRuleException("PUR-INVOICE-APPROVAL-REQUIRED", "دورة المشتريات المفعّلة تتطلب اعتماد فاتورة الشراء قبل ترحيلها.");
        }

        invoice.Status = PurchaseInvoiceStatus.Posted;

        foreach (var line in invoice.Lines)
        {
            db.SupplierPriceHistories.Add(new SupplierPriceHistory
            {
                CompanyId = invoice.CompanyId,
                SupplierId = invoice.SupplierId,
                ItemId = line.ItemId,
                UnitPrice = line.UnitPrice,
                UnitId = line.UnitId,
                EffectiveDate = invoice.InvoiceDate,
                PurchaseInvoiceId = invoice.Id
            });
        }

        if (cycleSettings?.AutoCreateReceiptOnInvoicePost == true)
        {
            await CreateReceiptForInvoiceAsync(invoice, cancellationToken);
        }
        else if (cycleSettings?.RequiresGoodsReceipt == true && invoice.PurchaseOrderId is { } orderBehindInvoice)
        {
            // Remarks4 item 6 — PurchaseCycleSettings.RequiresGoodsReceipt: goods bought on an order
            // arrive before their bill, so an invoice against that order may only post once somebody
            // confirmed they turned up. It is deliberately not enforced on an invoice with no order:
            // in the direct cycle the goods are received against the invoice, which cannot exist
            // before the invoice is posted — the auto-create flag above is that cycle's answer.
            var received = await db.GoodsReceipts.AnyAsync(
                r => r.PurchaseInvoiceId == invoice.Id || r.PurchaseOrderId == orderBehindInvoice, cancellationToken);
            if (!received)
            {
                throw new BusinessRuleException(
                    "PUR-INVOICE-RECEIPT-REQUIRED",
                    "دورة المشتريات المفعّلة تتطلب إذن إضافة قبل ترحيل الفاتورة — سجّل استلام البضاعة الأول أو اقفل الإعداد من إعدادات دورة المشتريات.");
            }
        }

        await PostJournalEntryAsync(invoice, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The invoice's journal entry, built from the company's PURCHASING_PURCHASE_INVOICE template
    /// (Docs/Posting-Engine-Implementation-Plan.md, stage 2). Saved in the same SaveChanges as the
    /// invoice, so either both land or neither does.
    ///
    /// Always credits the supplier, whatever the payment terms. Paying — cash included — is a
    /// supplier payment voucher, which already posts Dr supplier / Cr treasury; an invoice that
    /// credited the treasury itself would have the voucher take the same cash out a second time.
    ///
    /// The fields below are what a template can reference. AdditionalCosts and CommissionAmount are
    /// offered but are not part of TotalAmount: they are owed to other parties, not the supplier.
    /// </summary>
    private async Task PostJournalEntryAsync(PurchaseInvoice invoice, CancellationToken cancellationToken)
    {
        var companyId = invoice.CompanyId!.Value;
        var discount = invoice.DiscountAmount ?? 0m;
        invoice.JournalEntry = await postingEngine.PostIfConfiguredAsync(new TemplatePostingRequest
        {
            CompanyId = companyId,
            BranchId = invoice.BranchId,
            ScreenCode = PostingKeys.PurchaseInvoiceScreen,
            SourceModule = SourceModule.Purchasing,
            SourceDocumentType = SourceDocumentType.Invoice,
            SourceDocumentId = invoice.Id,
            EntryDate = invoice.InvoiceDate,
            Description = $"فاتورة شراء {invoice.InvoiceNumber}"
                + (invoice.SupplierInvoiceNumber is null ? string.Empty : $" — فاتورة المورد {invoice.SupplierInvoiceNumber}"),
            CurrencyCode = invoice.CurrencyCode,
            ExchangeRate = invoice.ExchangeRate,
            IdempotencyKey = PostingKeys.For(companyId, "PurchaseInvoice.Post", invoice.Id),
            Context = PostingContext.Create(
                new Dictionary<string, object?>
                {
                    ["SupplierId"] = invoice.SupplierId,
                    ["PaymentTerms"] = invoice.PaymentTerms.ToString(),
                    ["BranchId"] = invoice.BranchId,
                    ["WarehouseId"] = invoice.WarehouseId,
                    ["Subtotal"] = invoice.Subtotal,
                    ["DiscountAmount"] = discount,
                    ["NetAmount"] = invoice.Subtotal - discount,
                    ["TaxAmount"] = invoice.TaxAmount,
                    ["TotalAmount"] = invoice.TotalAmount,
                    ["AdditionalCosts"] = invoice.AdditionalCosts,
                    ["CommissionAmount"] = invoice.CommissionAmount ?? 0m,
                    ["CommissionAccountId"] = invoice.CommissionAccountId
                },
                invoice.Lines.Select(l => new PostingContextLine(l.ItemId, l.BaseQuantity, l.BaseUnitCost, l.BaseUnitCost)))
        }, cancellationToken);
    }

    /// <summary>
    /// Drafts the goods receipt that matches a freshly posted invoice.
    ///
    /// Draft, not posted: the invoice says what was bought, but only the warehouse can say what
    /// physically turned up and in what condition. Posting it here would move stock nobody had
    /// eyes on, and would pre-empt the accepted/rejected split the receipt exists to record.
    ///
    /// The receipt needs a warehouse the invoice itself may not name, so it falls back to the
    /// supplier's default. With neither, the flag cannot be honoured and that is worth saying out
    /// loud rather than silently skipping — an operator who switched it on expects a receipt.
    /// </summary>
    private async Task CreateReceiptForInvoiceAsync(PurchaseInvoice invoice, CancellationToken cancellationToken)
    {
        if (await db.GoodsReceipts.AnyAsync(r => r.PurchaseInvoiceId == invoice.Id, cancellationToken))
        {
            return;
        }

        var receivable = invoice.Lines.Where(l => l.Quantity > l.ReceivedQuantity).ToList();
        if (receivable.Count == 0)
        {
            return;
        }

        var warehouseId = invoice.WarehouseId;
        if (warehouseId is null)
        {
            warehouseId = await db.Suppliers
                .Where(s => s.Id == invoice.SupplierId)
                .Select(s => s.DefaultWarehouseId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (warehouseId is null)
        {
            throw new BusinessRuleException(
                "PUR-INVOICE-RECEIPT-NO-WAREHOUSE",
                "الإنشاء التلقائي لإذن الإضافة مفعَّل، لكن لا الفاتورة ولا المورد محدَّد له مخزن استلام.");
        }

        var receiptNumber = await codeGenerator.ResolveCodeAsync("PURCHASING_GOODS_RECEIPT", null, cancellationToken);

        var receipt = new GoodsReceipt
        {
            CompanyId = invoice.CompanyId,
            BranchId = invoice.BranchId,
            WarehouseId = warehouseId.Value,
            ReceiptNumber = receiptNumber,
            ReceiptDate = invoice.InvoiceDate,
            SupplierId = invoice.SupplierId,
            PurchaseOrderId = invoice.PurchaseOrderId,
            PurchaseInvoiceId = invoice.Id,
            Status = GoodsReceiptStatus.Draft,
            Notes = $"إذن تلقائي من فاتورة الشراء {invoice.InvoiceNumber}"
        };

        var lineNumber = 1;
        foreach (var line in receivable)
        {
            var outstanding = line.Quantity - line.ReceivedQuantity;

            // Everything is proposed as accepted; the warehouse edits this Draft to record whatever
            // was actually rejected before posting it.
            receipt.Lines.Add(new GoodsReceiptLine
            {
                LineNumber = lineNumber++,
                ItemId = line.ItemId,
                Quantity = outstanding,
                AcceptedQuantity = outstanding,
                RejectedQuantity = 0m,
                UnitCost = line.UnitPrice,
                UnitId = line.UnitId,
                UnitFactor = line.UnitFactor,
                BaseQuantity = ItemUnits.ToBase(outstanding, line.UnitFactor),
                BaseUnitCost = line.BaseUnitCost,
                ExpectedQuantity = outstanding,
                VarianceQuantity = 0m,
                QualityCheckStatus = GoodsReceiptQualityCheckStatus.Pending
            });
        }

        db.GoodsReceipts.Add(receipt);
    }
}
