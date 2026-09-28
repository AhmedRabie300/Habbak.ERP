using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.SalesInvoices.Dtos;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesInvoices.Commands.CreateSalesInvoice;

/// <summary>
/// Creates a sales invoice as Draft (screen #7).
///
/// Rule SalesCycleSettings.RequiresOrderBeforeInvoice: when true (default), a SourceOrderId is
/// required — mirrors CreateSalesOrderCommand's own RequiresQuoteBeforeOrder check. A linked order
/// must be Confirmed (not Draft/Rejected/Cancelled), same discipline as
/// CreatePurchaseInvoiceCommand's PurchaseOrder status check.
///
/// Rule 1/3: a Credit invoice is checked against the customer's outstanding balance (Σ of Posted
/// invoices' TotalAmount − AmountPaid) plus this invoice's own total, against Customer.CreditLimit.
/// Exceeding it is rejected unless the caller sends CreditLimitOverrideApproved = true — there is no
/// permission-check infrastructure anywhere in this codebase yet (no IPermissionService, no
/// [Authorize(Policy)] beyond plain authentication), so the "OverrideCreditLimit صلاحية خاصة" half of
/// rule 1 cannot be technically enforced; only the explicit-confirmation half is.
/// </summary>
public sealed record CreateSalesInvoiceCommand : IRequest<long>
{
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public long? SourceOrderId { get; init; }
    public required SalesInvoicePaymentType PaymentType { get; init; }
    public bool CreditLimitOverrideApproved { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal? DiscountAmount { get; init; }
    public required IReadOnlyList<SalesInvoiceLineInput> Lines { get; init; }
}

public sealed class CreateSalesInvoiceCommandValidator : AbstractValidator<CreateSalesInvoiceCommand>
{
    public CreateSalesInvoiceCommandValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.InvoiceDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("فاتورة المبيعات تحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CreateSalesInvoiceCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator, IUserAccessService access)
    : IRequestHandler<CreateSalesInvoiceCommand, long>
{
    public async Task<long> Handle(CreateSalesInvoiceCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var cycleSettings = await db.SalesCycleSettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);
        if (cycleSettings?.RequiresOrderBeforeInvoice != false && request.SourceOrderId is null)
        {
            throw new BusinessRuleException("SALES-INVOICE-ORDER-REQUIRED", "دورة المبيعات المفعّلة تتطلب أمر بيع قبل إنشاء فاتورة المبيعات.");
        }

        if (request.SourceOrderId is { } sourceOrderId)
        {
            var orderStatus = await db.SalesOrders
                .Where(o => o.Id == sourceOrderId)
                .Select(o => (SalesOrderStatus?)o.Status)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException(nameof(SalesOrder), sourceOrderId);

            if (orderStatus is not (SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyDelivered or SalesOrderStatus.Delivered))
            {
                throw new BusinessRuleException("SALES-INVOICE-ORDER-NOT-CONFIRMED", "لا يمكن إنشاء فاتورة مبيعات مرتبطة بأمر بيع لم يُؤكَّد بعد.");
            }
        }

        // A role that may not set the discount on this screen creates the invoice without one — the
        // invoice's and its lines' (the masking hides both, they share the property name).
        var discountEditable = await access.HasFieldPermissionOnScreenAsync(null, "SalesInvoice", "DiscountAmount", FieldAction.Edit, cancellationToken);
        var discount = discountEditable ? request.DiscountAmount : null;
        var inputs = discountEditable ? request.Lines : request.Lines.Select(l => l with { DiscountAmount = null }).ToList();
        var (lines, subtotal) = SalesInvoiceLineBuilder.Build(inputs);
        var totalAmount = subtotal - (discount ?? 0) + request.TaxAmount;

        if (request.PaymentType == SalesInvoicePaymentType.Credit)
        {
            var outstandingBalance = await db.SalesInvoices
                .Where(i => i.CustomerId == request.CustomerId && i.Status == SalesInvoiceStatus.Posted)
                .SumAsync(i => i.TotalAmount - i.AmountPaid, cancellationToken);

            if (outstandingBalance + totalAmount > customer.CreditLimit && !request.CreditLimitOverrideApproved)
            {
                throw new BusinessRuleException(
                    "SALES-INVOICE-CREDIT-LIMIT-EXCEEDED",
                    $"تجاوز حد الائتمان — الرصيد المستحق الحالي {outstandingBalance:F2} + قيمة الفاتورة {totalAmount:F2} يتجاوز حد ائتمان العميل {customer.CreditLimit:F2}.");
            }
        }

        var invoiceNumber = await codeGenerator.ResolveCodeAsync("SALES_INVOICE", null, cancellationToken);

        var invoice = new SalesInvoice
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = request.InvoiceDate,
            SourceOrderId = request.SourceOrderId,
            PaymentType = request.PaymentType,
            CreditLimitOverrideApproved = request.PaymentType == SalesInvoicePaymentType.Credit && request.CreditLimitOverrideApproved,
            Status = SalesInvoiceStatus.Draft,
            Subtotal = subtotal,
            DiscountAmount = discount,
            TaxAmount = request.TaxAmount,
            TotalAmount = totalAmount,
            AmountPaid = 0
        };

        foreach (var line in lines)
        {
            invoice.Lines.Add(line);
        }

        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}
