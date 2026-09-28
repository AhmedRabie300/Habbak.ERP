using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.GoodsReceipts.Dtos;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.GoodsReceipts.Commands.CreateGoodsReceipt;

/// <summary>
/// Creates a goods receipt as Draft (screen #6), copied from whichever document the goods were
/// bought on: a Confirmed/PartiallyReceived <see cref="PurchaseOrder"/> (rule 2's auto-copy), or a
/// posted <see cref="PurchaseInvoice"/> in the "invoice only" cycle where no order exists. Exactly
/// one of the two must be supplied.
///
/// ExpectedQuantity/VarianceQuantity are computed against each line's still-outstanding quantity
/// (source line's Quantity − ReceivedQuantity at the moment this receipt is created), not its
/// original quantity, so a second partial receipt against the same source gets a variance that
/// means something.
/// </summary>
public sealed record CreateGoodsReceiptCommand : IRequest<long>
{
    public long? BranchId { get; init; }
    public required long WarehouseId { get; init; }
    public required DateOnly ReceiptDate { get; init; }
    public long? PurchaseOrderId { get; init; }
    public long? PurchaseInvoiceId { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<GoodsReceiptLineInput> Lines { get; init; }
}

public sealed class CreateGoodsReceiptCommandValidator : AbstractValidator<CreateGoodsReceiptCommand>
{
    public CreateGoodsReceiptCommandValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.ReceiptDate).NotEqual(default(DateOnly));
        RuleFor(x => x)
            .Must(x => (x.PurchaseOrderId is > 0) ^ (x.PurchaseInvoiceId is > 0))
            .WithMessage("إذن الإضافة لازم يرتبط بأمر شراء أو بفاتورة شراء — واحد منهم بالظبط.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("إذن الإضافة يحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.AcceptedQuantity).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.RejectedQuantity).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.UnitCost).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.UnitId).GreaterThan(0).When(l => l.UnitId is not null);

            line.RuleFor(l => l)
                .Must(l => l.AcceptedQuantity + l.RejectedQuantity == l.Quantity)
                .WithMessage("الكمية المقبولة + الكمية المرفوضة لازم تساوي الكمية المستلمة فعليًا.");

            line.RuleFor(l => l.RejectedReason)
                .NotEmpty().When(l => l.RejectedQuantity > 0)
                .WithMessage("سبب الرفض إلزامي لوجود كمية مرفوضة.");
            line.RuleFor(l => l.RejectedWarehouseId)
                .NotNull().When(l => l.RejectedQuantity > 0)
                .WithMessage("مخزن التالف/المرتجعات إلزامي لوجود كمية مرفوضة.");
        });
    }
}

public sealed class CreateGoodsReceiptCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateGoodsReceiptCommand, long>
{
    public async Task<long> Handle(CreateGoodsReceiptCommand request, CancellationToken cancellationToken)
    {
        await EnsureCycleAllowsAsync(request, cancellationToken);

        var source = await ResolveSourceAsync(request, cancellationToken);

        var receiptNumber = await codeGenerator.ResolveCodeAsync("PURCHASING_GOODS_RECEIPT", null, cancellationToken);

        var receipt = new GoodsReceipt
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            WarehouseId = request.WarehouseId,
            ReceiptNumber = receiptNumber,
            ReceiptDate = request.ReceiptDate,
            SupplierId = source.SupplierId,
            PurchaseOrderId = request.PurchaseOrderId,
            PurchaseInvoiceId = request.PurchaseInvoiceId,
            Status = GoodsReceiptStatus.Draft,
            Notes = request.Notes
        };

        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ItemId), cancellationToken);
        var lineNumber = 1;
        foreach (var input in request.Lines)
        {
            var unit = units.Resolve(input.ItemId, input.UnitId, PurchaseUnits.NotAllowed);

            var item = await db.Items.FirstOrDefaultAsync(i => i.Id == input.ItemId, cancellationToken)
                ?? throw new NotFoundException(nameof(Item), input.ItemId);

            if (item.IsTracked && string.IsNullOrWhiteSpace(input.BatchNumber))
            {
                throw new BusinessRuleException("PUR-RECEIPT-BATCH-REQUIRED", $"الصنف رقم {input.ItemId} يتطلب رقم دفعة إلزاميًا.");
            }

            if (!source.OutstandingBaseByItemId.TryGetValue(input.ItemId, out var outstandingBase))
            {
                throw new BusinessRuleException("PUR-RECEIPT-ITEM-NOT-IN-ORDER", $"الصنف رقم {input.ItemId} غير موجود في {source.SourceLabel} المرتبط.");
            }

            // What is still due, in this receipt line's unit (the order may be in another unit).
            var expectedQuantity = Math.Round(outstandingBase / unit.Factor, 4);

            var varianceQuantity = input.Quantity - expectedQuantity;

            if (varianceQuantity != 0 && string.IsNullOrWhiteSpace(input.VarianceReason))
            {
                throw new BusinessRuleException("PUR-RECEIPT-VARIANCE-REASON-REQUIRED", $"سبب الفرق إلزامي للصنف رقم {input.ItemId}.");
            }

            receipt.Lines.Add(new GoodsReceiptLine
            {
                LineNumber = lineNumber++,
                ItemId = input.ItemId,
                Quantity = input.Quantity,
                AcceptedQuantity = input.AcceptedQuantity,
                RejectedQuantity = input.RejectedQuantity,
                RejectedReason = input.RejectedReason,
                RejectedWarehouseId = input.RejectedWarehouseId,
                UnitCost = input.UnitCost,
                UnitId = unit.UnitId,
                UnitFactor = unit.Factor,
                BaseQuantity = ItemUnits.ToBase(input.Quantity, unit.Factor),
                BaseUnitCost = ItemUnits.CostPerBase(input.UnitCost, unit.Factor),
                ExpectedQuantity = expectedQuantity,
                VarianceQuantity = varianceQuantity,
                VarianceReason = input.VarianceReason,
                BatchNumber = input.BatchNumber,
                ExpiryDate = input.ExpiryDate,
                QualityCheckStatus = Enum.Parse<GoodsReceiptQualityCheckStatus>(input.QualityCheckStatus),
                QualityCheckNotes = input.QualityCheckNotes
            });
        }

        db.GoodsReceipts.Add(receipt);
        await db.SaveChangesAsync(cancellationToken);

        return receipt.Id;
    }

    /// <summary>What the receipt is being copied from, flattened so the line loop does not care
    /// which of the two documents it was.</summary>
    private sealed record ReceiptSource(long SupplierId, IReadOnlyDictionary<long, decimal> OutstandingBaseByItemId, string SourceLabel);

    /// <summary>
    /// Remarks4 item 6 — the two cycle settings that govern which document a receipt may come off:
    /// RequiresPurchaseOrder (goods are received against an order, so an invoice with no order
    /// behind it cannot be received either) and AllowReceiptWithoutInvoice (when off, the paperwork
    /// must arrive before the goods are booked in).
    /// </summary>
    private async Task EnsureCycleAllowsAsync(CreateGoodsReceiptCommand request, CancellationToken cancellationToken)
    {
        var cycleSettings = await db.PurchaseCycleSettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);
        if (cycleSettings is null)
        {
            return;
        }

        if (cycleSettings.RequiresPurchaseOrder && request.PurchaseOrderId is null && request.PurchaseInvoiceId is { } invoiceId)
        {
            var orderId = await db.PurchaseInvoices.Where(i => i.Id == invoiceId).Select(i => i.PurchaseOrderId).FirstOrDefaultAsync(cancellationToken);
            if (orderId is null)
            {
                throw new BusinessRuleException(
                    "PUR-RECEIPT-ORDER-REQUIRED", "دورة المشتريات المفعّلة تتطلب أمر شراء — الفاتورة دي مالهاش أمر.");
            }
        }

        if (!cycleSettings.AllowReceiptWithoutInvoice && request.PurchaseInvoiceId is null && request.PurchaseOrderId is { } sourceOrderId)
        {
            var invoiced = await db.PurchaseInvoices.AnyAsync(i => i.PurchaseOrderId == sourceOrderId, cancellationToken);
            if (!invoiced)
            {
                throw new BusinessRuleException(
                    "PUR-RECEIPT-INVOICE-REQUIRED", "دورة المشتريات المفعّلة ماتسمحش باستلام بضاعة قبل تسجيل فاتورتها.");
            }
        }
    }

    private async Task<ReceiptSource> ResolveSourceAsync(CreateGoodsReceiptCommand request, CancellationToken cancellationToken)
    {
        if (request.PurchaseOrderId is { } purchaseOrderId)
        {
            var order = await db.PurchaseOrders
                .Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.Id == purchaseOrderId, cancellationToken)
                ?? throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);

            // Goods can turn up after the bill, so a billed order still receives (Remarks6).
            if (order.Status is not (PurchaseOrderStatus.Confirmed or PurchaseOrderStatus.PartiallyReceived
                or PurchaseOrderStatus.PartiallyInvoiced or PurchaseOrderStatus.Invoiced))
            {
                throw new BusinessRuleException("PUR-RECEIPT-ORDER-NOT-RECEIVABLE", "لا يمكن إنشاء إذن إضافة إلا من أمر شراء مؤكد أو مستلم جزئيًا.");
            }

            return new ReceiptSource(
                order.SupplierId,
                order.Lines.ToDictionary(l => l.ItemId, l => ItemUnits.ToBase(l.Quantity - l.ReceivedQuantity, l.UnitFactor)),
                "أمر الشراء");
        }

        var invoiceId = request.PurchaseInvoiceId!.Value;
        var invoice = await db.PurchaseInvoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseInvoice), invoiceId);

        // A Draft invoice is still being edited — its quantities are not yet an agreement to
        // receive against. Cancelled/Rejected ones never were.
        if (invoice.Status is PurchaseInvoiceStatus.Draft or PurchaseInvoiceStatus.Cancelled or PurchaseInvoiceStatus.Rejected)
        {
            throw new BusinessRuleException("PUR-RECEIPT-INVOICE-NOT-RECEIVABLE", "لا يمكن إنشاء إذن إضافة إلا من فاتورة شراء مُرحَّلة.");
        }

        return new ReceiptSource(
            invoice.SupplierId,
            invoice.Lines.ToDictionary(l => l.ItemId, l => ItemUnits.ToBase(l.Quantity - l.ReceivedQuantity, l.UnitFactor)),
            "فاتورة الشراء");
    }
}
