using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.CreatePurchaseOrder;

/// <summary>
/// Creates a purchase order as Draft (screen #4). Rule 3: SupplierId is always required.
///
/// Rule PurchaseCycleSettings.RequiresPurchaseRequest: if the company's configured cycle requires
/// a purchase request first, this rejects creation without one — the first real enforcement point
/// of the configurable cycle (RequiresQuotation isn't checked yet since RFQ doesn't exist).
///
/// Linking an approved PurchaseRequestId auto-copies nothing by itself (the caller already sends
/// the lines it wants, typically pre-filled from the request by the frontend) — this command's own
/// job is just to mark the source request Converted (rule 2's "علاقة مرجعية" — SourceDocumentType/Id
/// equivalent via PurchaseOrder.PurchaseRequestId here).
/// </summary>
public sealed record CreatePurchaseOrderCommand : IRequest<long>
{
    public long? BranchId { get; init; }
    public required DateOnly OrderDate { get; init; }
    public required long SupplierId { get; init; }
    public long? PurchaseRequestId { get; init; }

    /// <summary>The awarded RFQ this order came from — required when the cycle requires quotations.</summary>
    public long? RFQId { get; init; }

    public required string CurrencyCode { get; init; }
    public required decimal ExchangeRate { get; init; }

    /// <summary>Left null, the terms fall back to the supplier's, then to PurchaseCycleSettings.DefaultPaymentTerms.</summary>
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

public sealed class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
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

public sealed class CreatePurchaseOrderCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreatePurchaseOrderCommand, long>
{
    public async Task<long> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
        {
            throw new NotFoundException("Supplier", request.SupplierId);
        }

        var cycleSettings = await db.PurchaseCycleSettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);
        if (cycleSettings?.RequiresPurchaseRequest == true && request.PurchaseRequestId is null)
        {
            throw new BusinessRuleException("PUR-ORDER-REQUEST-REQUIRED", "دورة المشتريات المفعّلة تتطلب طلب شراء قبل إنشاء أمر الشراء.");
        }

        // Remarks4 item 6 — PurchaseCycleSettings.RequiresQuotation: the order must come off an RFQ
        // this supplier actually won, not just any RFQ.
        if (cycleSettings?.RequiresQuotation == true && request.RFQId is null)
        {
            throw new BusinessRuleException("PUR-ORDER-RFQ-REQUIRED", "دورة المشتريات المفعّلة تتطلب طلب عروض أسعار قبل إنشاء أمر الشراء.");
        }

        if (request.RFQId is { } rfqId)
        {
            var rfq = await db.RequestsForQuotation
                .Include(r => r.Suppliers).ThenInclude(s => s.Quotes)
                .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken)
                ?? throw new NotFoundException(nameof(RequestForQuotation), rfqId);

            if (rfq.Status != RFQStatus.Awarded)
            {
                throw new BusinessRuleException("PUR-ORDER-RFQ-NOT-AWARDED", "لا يمكن إنشاء أمر شراء من طلب عروض أسعار لم يُرسَ بعد.");
            }

            // The award lives on the quotes (RFQSupplierQuote.IsSelected), so the winning supplier
            // is whoever owns at least one selected quote on this RFQ.
            if (!rfq.Suppliers.Any(s => s.SupplierId == request.SupplierId && s.Quotes.Any(q => q.IsSelected)))
            {
                throw new BusinessRuleException("PUR-ORDER-RFQ-SUPPLIER-MISMATCH", "المورد ده مش المورد اللي رسا عليه طلب عروض الأسعار.");
            }
        }

        PurchaseRequest? sourceRequest = null;
        if (request.PurchaseRequestId is { } purchaseRequestId)
        {
            sourceRequest = await db.PurchaseRequests.FirstOrDefaultAsync(r => r.Id == purchaseRequestId, cancellationToken)
                ?? throw new NotFoundException(nameof(PurchaseRequest), purchaseRequestId);

            // Remarks7: PartiallyConverted joins Approved — a request that was already ordered in part
            // is exactly what the next order converts the rest of.
            if (sourceRequest.Status is not (PurchaseRequestStatus.Approved or PurchaseRequestStatus.PartiallyConverted))
            {
                throw new BusinessRuleException("PUR-ORDER-REQUEST-NOT-APPROVED", "لا يمكن إنشاء أمر شراء من طلب غير معتمد.");
            }
        }

        // Remarks4 item 6 — PurchaseCycleSettings.DefaultPaymentTerms: what the company buys on when
        // neither the order nor the supplier says otherwise.
        var paymentTerms = request.PaymentTerms
            ?? await db.Suppliers.Where(s => s.Id == request.SupplierId).Select(s => (SupplierPaymentTerms?)s.PaymentTerms).FirstOrDefaultAsync(cancellationToken)
            ?? cycleSettings?.DefaultPaymentTerms
            ?? SupplierPaymentTerms.Net30;

        var orderNumber = await codeGenerator.ResolveCodeAsync("PURCHASING_PURCHASE_ORDER", null, cancellationToken);
        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ItemId), cancellationToken);
        var (lines, subtotal) = PurchaseOrderLineBuilder.Build(request.Lines, units);

        var order = new PurchaseOrder
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            OrderNumber = orderNumber,
            OrderDate = request.OrderDate,
            SupplierId = request.SupplierId,
            PurchaseRequestId = request.PurchaseRequestId,
            RFQId = request.RFQId,
            CurrencyCode = request.CurrencyCode,
            ExchangeRate = request.ExchangeRate,
            PaymentTerms = paymentTerms,
            DeliveryTerms = request.DeliveryTerms,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            DeliveryAddress = request.DeliveryAddress,
            Status = PurchaseOrderStatus.Draft,
            Subtotal = subtotal,
            TaxAmount = request.TaxAmount,
            TotalAmount = subtotal - (request.DiscountAmount ?? 0) + request.TaxAmount,
            DiscountAmount = request.DiscountAmount,
            DiscountReason = request.DiscountReason,
            Notes = request.Notes
        };

        foreach (var line in lines)
        {
            order.Lines.Add(line);
        }

        db.PurchaseOrders.Add(order);

        // Remarks7: what this order takes off its request, checked before anything is written — over
        // the remaining quantity, or a manual line where the company does not allow one, and the order
        // is refused rather than half-created.
        var requestPlan = await PurchaseOrderRequestLinking.PlanAsync(
            db, order, order.Lines.ToList(), cycleSettings?.AllowManualOrderLines ?? true, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        if (order.PurchaseRequestId is { } convertedRequestId)
        {
            await PurchaseOrderRequestLinking.ApplyAsync(db, convertedRequestId, requestPlan, 1, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return order.Id;
    }
}
