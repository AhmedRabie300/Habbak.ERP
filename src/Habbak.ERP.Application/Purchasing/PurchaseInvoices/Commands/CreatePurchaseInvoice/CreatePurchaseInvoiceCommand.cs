using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseInvoices.Commands.CreatePurchaseInvoice;

/// <summary>
/// Creates a purchase invoice as Draft (screen #5).
///
/// Rule PurchaseCycleSettings.AllowInvoiceWithoutOrder: when false (default), a PurchaseOrderId is
/// required — the same enforcement pattern already applied to RequiresPurchaseRequest on
/// CreatePurchaseOrderCommand. Rule 4 of the spec ("لا يمكن ترحيل فاتورة شراء بدون استلام" gated by
/// a setting named AllowInvoiceWithoutReceipt) is NOT enforced here: no such field was ever added to
/// PurchaseCycleSettings (section 2.3 only defines AllowInvoiceWithoutOrder/AllowReceiptWithoutInvoice)
/// and GoodsReceipt itself carries no PurchaseInvoiceId back-reference — treated as a spec
/// inconsistency rather than a gap to silently paper over with a new field nothing else reads.
/// </summary>
public sealed record CreatePurchaseInvoiceCommand : IRequest<long>
{
    public long? BranchId { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required DateOnly DueDate { get; init; }
    public required long SupplierId { get; init; }
    public string? SupplierInvoiceNumber { get; init; }
    public long? PurchaseOrderId { get; init; }
    public long? GoodsReceiptId { get; init; }
    public long? WarehouseId { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal ExchangeRate { get; init; }
    /// <summary>Left null, the terms fall back to the supplier's, then to PurchaseCycleSettings.DefaultPaymentTerms.</summary>
    public SupplierPaymentTerms? PaymentTerms { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal? DiscountAmount { get; init; }
    public string? DiscountReason { get; init; }
    public decimal AdditionalCosts { get; init; }
    public CostAllocationMethod? AdditionalCostAllocationMethod { get; init; }
    public decimal? CommissionRate { get; init; }
    public decimal? CommissionAmount { get; init; }
    public long? CommissionAccountId { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<PurchaseInvoiceLineInput> Lines { get; init; }
}

public sealed class CreatePurchaseInvoiceCommandValidator : AbstractValidator<CreatePurchaseInvoiceCommand>
{
    public CreatePurchaseInvoiceCommandValidator()
    {
        RuleFor(x => x.SupplierId).GreaterThan(0);
        RuleFor(x => x.InvoiceDate).NotEqual(default(DateOnly));
        RuleFor(x => x.DueDate).NotEqual(default(DateOnly));
        RuleFor(x => x.CurrencyCode).NotEmpty().MaximumLength(3);
        RuleFor(x => x.ExchangeRate).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("فاتورة الشراء تحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.UnitId).GreaterThan(0).When(l => l.UnitId is not null);
        });
    }
}

public sealed class CreatePurchaseInvoiceCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreatePurchaseInvoiceCommand, long>
{
    public async Task<long> Handle(CreatePurchaseInvoiceCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
        {
            throw new NotFoundException("Supplier", request.SupplierId);
        }

        var cycleSettings = await db.PurchaseCycleSettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);
        if (cycleSettings?.AllowInvoiceWithoutOrder != true && request.PurchaseOrderId is null)
        {
            throw new BusinessRuleException("PUR-INVOICE-ORDER-REQUIRED", "دورة المشتريات المفعّلة تتطلب أمر شراء قبل إنشاء فاتورة الشراء.");
        }

        if (request.PurchaseOrderId is { } purchaseOrderId)
        {
            var purchaseOrderStatus = await db.PurchaseOrders
                .Where(o => o.Id == purchaseOrderId)
                .Select(o => (PurchaseOrderStatus?)o.Status)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);

            // Mirrors CreateGoodsReceiptCommand's own check (Confirmed/PartiallyReceived) plus
            // FullyReceived — an order the supplier never confirmed (Draft/Sent) or that was
            // Cancelled/Rejected carries no agreed price/quantity to invoice against.
            // PartiallyInvoiced joins the list with Remarks6: an order that was billed in part is
            // exactly what the next invoice bills the rest of.
            if (purchaseOrderStatus is not (PurchaseOrderStatus.Confirmed or PurchaseOrderStatus.PartiallyReceived
                or PurchaseOrderStatus.FullyReceived or PurchaseOrderStatus.PartiallyInvoiced))
            {
                throw new BusinessRuleException("PUR-INVOICE-ORDER-NOT-CONFIRMED", "لا يمكن إنشاء فاتورة شراء مرتبطة بأمر شراء لم يُعتمَد بعد — يجب أن يكون أمر الشراء بحالة مؤكد أو مُستلم (جزئيًا/كليًا).");
            }
        }

        if (request.GoodsReceiptId is { } goodsReceiptId
            && !await db.GoodsReceipts.AnyAsync(r => r.Id == goodsReceiptId, cancellationToken))
        {
            throw new NotFoundException(nameof(GoodsReceipt), goodsReceiptId);
        }

        // Remarks4 item 6 — PurchaseCycleSettings.DefaultPaymentTerms: what the company buys on when
        // neither the invoice nor the supplier says otherwise.
        var paymentTerms = request.PaymentTerms
            ?? await db.Suppliers.Where(s => s.Id == request.SupplierId).Select(s => (SupplierPaymentTerms?)s.PaymentTerms).FirstOrDefaultAsync(cancellationToken)
            ?? cycleSettings?.DefaultPaymentTerms
            ?? SupplierPaymentTerms.Net30;

        var invoiceNumber = await codeGenerator.ResolveCodeAsync("PURCHASING_PURCHASE_INVOICE", null, cancellationToken);
        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ItemId), cancellationToken);
        var (lines, subtotal) = PurchaseInvoiceLineBuilder.Build(request.Lines, request.AdditionalCosts, request.AdditionalCostAllocationMethod, units);

        var invoice = new PurchaseInvoice
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = request.InvoiceDate,
            DueDate = request.DueDate,
            SupplierId = request.SupplierId,
            SupplierInvoiceNumber = request.SupplierInvoiceNumber,
            PurchaseOrderId = request.PurchaseOrderId,
            GoodsReceiptId = request.GoodsReceiptId,
            WarehouseId = request.WarehouseId,
            CurrencyCode = request.CurrencyCode,
            ExchangeRate = request.ExchangeRate,
            PaymentTerms = paymentTerms,
            Status = PurchaseInvoiceStatus.Draft,
            Subtotal = subtotal,
            TaxAmount = request.TaxAmount,
            TotalAmount = subtotal - (request.DiscountAmount ?? 0) + request.TaxAmount,
            DiscountAmount = request.DiscountAmount,
            DiscountReason = request.DiscountReason,
            AdditionalCosts = request.AdditionalCosts,
            AdditionalCostAllocationMethod = request.AdditionalCostAllocationMethod,
            CommissionRate = request.CommissionRate,
            CommissionAmount = request.CommissionAmount,
            CommissionAccountId = request.CommissionAccountId,
            Notes = request.Notes
        };

        foreach (var line in lines)
        {
            invoice.Lines.Add(line);
        }

        db.PurchaseInvoices.Add(invoice);

        // Remarks6: what this invoice takes off its order, checked before anything is written —
        // over the remaining quantity, or a manual line where the company does not allow one, and
        // the invoice is refused rather than half-created.
        var plan = await PurchaseOrderInvoicing.PlanAsync(
            db, invoice, invoice.Lines.ToList(), cycleSettings?.AllowManualInvoiceLines ?? true, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        if (invoice.PurchaseOrderId is { } billedOrderId)
        {
            await PurchaseOrderInvoicing.ApplyAsync(db, billedOrderId, plan, 1, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return invoice.Id;
    }
}
