using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Domain.Purchasing;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseInvoices.Commands.UpdatePurchaseInvoice;

/// <summary>Edits a Draft purchase invoice — rule 15: no editing after Posted (Draft only).</summary>
public sealed record UpdatePurchaseInvoiceCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public long? BranchId { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required DateOnly DueDate { get; init; }
    public required long SupplierId { get; init; }
    public string? SupplierInvoiceNumber { get; init; }
    public long? PurchaseOrderId { get; init; }
    public long? GoodsReceiptId { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal ExchangeRate { get; init; }
    /// <summary>Left null, the invoice keeps the terms it already has.</summary>
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

public sealed class UpdatePurchaseInvoiceCommandValidator : AbstractValidator<UpdatePurchaseInvoiceCommand>
{
    public UpdatePurchaseInvoiceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
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

public sealed class UpdatePurchaseInvoiceCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdatePurchaseInvoiceCommand>
{
    public async Task Handle(UpdatePurchaseInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.PurchaseInvoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseInvoice), request.Id);

        if (invoice.Status != PurchaseInvoiceStatus.Draft)
        {
            throw new BusinessRuleException("PUR-INVOICE-NOT-EDITABLE", "لا يمكن تعديل فاتورة الشراء إلا وهي في حالة مسودة.");
        }

        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
        {
            throw new NotFoundException("Supplier", request.SupplierId);
        }

        db.Entry(invoice).Property(nameof(PurchaseInvoice.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        invoice.BranchId = request.BranchId;
        invoice.InvoiceDate = request.InvoiceDate;
        invoice.DueDate = request.DueDate;
        invoice.SupplierId = request.SupplierId;
        invoice.SupplierInvoiceNumber = request.SupplierInvoiceNumber;
        invoice.PurchaseOrderId = request.PurchaseOrderId;
        invoice.GoodsReceiptId = request.GoodsReceiptId;
        invoice.CurrencyCode = request.CurrencyCode;
        invoice.ExchangeRate = request.ExchangeRate;
        invoice.PaymentTerms = request.PaymentTerms ?? invoice.PaymentTerms;
        invoice.TaxAmount = request.TaxAmount;
        invoice.DiscountAmount = request.DiscountAmount;
        invoice.DiscountReason = request.DiscountReason;
        invoice.AdditionalCosts = request.AdditionalCosts;
        invoice.AdditionalCostAllocationMethod = request.AdditionalCostAllocationMethod;
        invoice.CommissionRate = request.CommissionRate;
        invoice.CommissionAmount = request.CommissionAmount;
        invoice.CommissionAccountId = request.CommissionAccountId;
        invoice.Notes = request.Notes;

        // Two round trips: replacement lines reuse LineNumber 1, 2, 3... and the unique
        // (PurchaseInvoiceId, LineNumber) index is checked per-statement.
        // Built (units checked) before the old lines go, so a refused unit leaves the invoice as it was.
        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ItemId), cancellationToken);
        var (lines, subtotal) = PurchaseInvoiceLineBuilder.Build(request.Lines, request.AdditionalCosts, request.AdditionalCostAllocationMethod, units);

        // Remarks6: the invoice gives back what its current lines put on the order, so the new lines
        // are checked against a remaining quantity that does not count this invoice twice. Both the
        // order it used to bill and the one it bills now are brought up to date.
        var previousOrderId = invoice.PurchaseOrderId;
        var previousOrderLines = await PurchaseOrderInvoicing.LoadOrderLinesAsync(
            db, invoice.Lines.Where(l => l.PurchaseOrderLineId is not null).Select(l => l.PurchaseOrderLineId!.Value), cancellationToken);
        var previousPlan = PurchaseOrderInvoicing.PlanOf(invoice.Lines, previousOrderLines);
        foreach (var (orderLineId, quantity) in previousPlan)
        {
            if (previousOrderLines.TryGetValue(orderLineId, out var orderLine))
            {
                orderLine.InvoicedQuantity = Math.Max(0m, orderLine.InvoicedQuantity - quantity);
            }
        }

        var cycleSettings = await db.PurchaseCycleSettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);
        invoice.PurchaseOrderId = request.PurchaseOrderId;
        var plan = await PurchaseOrderInvoicing.PlanAsync(
            db, invoice, lines, cycleSettings?.AllowManualInvoiceLines ?? true, cancellationToken);

        db.PurchaseInvoiceLines.RemoveRange(invoice.Lines);
        invoice.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);
        foreach (var line in lines)
        {
            invoice.Lines.Add(line);
        }

        invoice.Subtotal = subtotal;
        invoice.TotalAmount = subtotal - (request.DiscountAmount ?? 0) + request.TaxAmount;

        if (request.PurchaseOrderId is { } newOrderId)
        {
            await PurchaseOrderInvoicing.ApplyAsync(db, newOrderId, plan, 1, cancellationToken);
        }

        if (previousOrderId is { } oldOrderId && oldOrderId != request.PurchaseOrderId)
        {
            await PurchaseOrderInvoicing.RefreshOrderStatusAsync(db, oldOrderId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
