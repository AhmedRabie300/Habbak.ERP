using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.UpdatePurchaseOrder;

/// <summary>Edits a Draft purchase order — rule 14: no editing after Confirmed (Draft/Sent only,
/// section 6.3).</summary>
public sealed record UpdatePurchaseOrderCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public long? BranchId { get; init; }
    public required DateOnly OrderDate { get; init; }
    public required long SupplierId { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal ExchangeRate { get; init; }
    /// <summary>Left null, the order keeps the terms it already has.</summary>
    public SupplierPaymentTerms? PaymentTerms { get; init; }
    public PurchaseOrderDeliveryTerms? DeliveryTerms { get; init; }
    public DateOnly? ExpectedDeliveryDate { get; init; }
    public string? DeliveryAddress { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal? DiscountAmount { get; init; }
    public string? DiscountReason { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<PurchaseOrderLineInput> Lines { get; init; }
}

public sealed class UpdatePurchaseOrderCommandValidator : AbstractValidator<UpdatePurchaseOrderCommand>
{
    public UpdatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.SupplierId).GreaterThan(0);
        RuleFor(x => x.OrderDate).NotEqual(default(DateOnly));
        RuleFor(x => x.CurrencyCode).NotEmpty().MaximumLength(3);
        RuleFor(x => x.ExchangeRate).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("أمر الشراء يحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.UnitId).GreaterThan(0).When(l => l.UnitId is not null);
        });
    }
}

public sealed class UpdatePurchaseOrderCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdatePurchaseOrderCommand>
{
    public async Task Handle(UpdatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.Id);

        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Sent))
        {
            throw new BusinessRuleException("PUR-ORDER-NOT-EDITABLE", "لا يمكن تعديل أمر الشراء إلا قبل تأكيده (مسودة أو مُرسل).");
        }

        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
        {
            throw new NotFoundException("Supplier", request.SupplierId);
        }

        db.Entry(order).Property(nameof(PurchaseOrder.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        order.BranchId = request.BranchId;
        order.OrderDate = request.OrderDate;
        order.SupplierId = request.SupplierId;
        order.CurrencyCode = request.CurrencyCode;
        order.ExchangeRate = request.ExchangeRate;
        order.PaymentTerms = request.PaymentTerms ?? order.PaymentTerms;
        order.DeliveryTerms = request.DeliveryTerms;
        order.ExpectedDeliveryDate = request.ExpectedDeliveryDate;
        order.DeliveryAddress = request.DeliveryAddress;
        order.TaxAmount = request.TaxAmount;
        order.DiscountAmount = request.DiscountAmount;
        order.DiscountReason = request.DiscountReason;
        order.Notes = request.Notes;

        // Two round trips: replacement lines reuse LineNumber 1, 2, 3... and the unique
        // (PurchaseOrderId, LineNumber) index is checked per-statement.
        // Built (units checked) before the old lines go, so a refused unit leaves the order as it was.
        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ItemId), cancellationToken);
        var (lines, subtotal) = PurchaseOrderLineBuilder.Build(request.Lines, units);

        // Remarks7: the order gives back what its current lines put on the request, so the new lines
        // are checked against a remaining quantity that does not count this order twice.
        var previousRequestLines = await PurchaseOrderRequestLinking.LoadRequestLinesAsync(
            db, order.Lines.Where(l => l.PurchaseRequestLineId is not null).Select(l => l.PurchaseRequestLineId!.Value), cancellationToken);
        var previousPlan = PurchaseOrderRequestLinking.PlanOf(order.Lines, previousRequestLines);
        foreach (var (requestLineId, quantity) in previousPlan)
        {
            if (previousRequestLines.TryGetValue(requestLineId, out var requestLine))
            {
                requestLine.OrderedQuantity = Math.Max(0m, requestLine.OrderedQuantity - quantity);
            }
        }

        var cycleSettings = await db.PurchaseCycleSettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);
        var requestPlan = await PurchaseOrderRequestLinking.PlanAsync(
            db, order, lines, cycleSettings?.AllowManualOrderLines ?? true, cancellationToken);

        db.PurchaseOrderLines.RemoveRange(order.Lines);
        order.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);
        foreach (var line in lines)
        {
            order.Lines.Add(line);
        }

        order.Subtotal = subtotal;
        order.TotalAmount = subtotal - (request.DiscountAmount ?? 0) + request.TaxAmount;

        if (order.PurchaseRequestId is { } requestId)
        {
            await PurchaseOrderRequestLinking.ApplyAsync(db, requestId, requestPlan, 1, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
