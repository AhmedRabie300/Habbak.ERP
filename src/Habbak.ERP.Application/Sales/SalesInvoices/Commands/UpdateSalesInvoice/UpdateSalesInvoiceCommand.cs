using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.SalesInvoices.Dtos;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesInvoices.Commands.UpdateSalesInvoice;

/// <summary>Edits a Draft sales invoice — only a Draft can change. Re-runs the same credit-limit
/// check as CreateSalesInvoiceCommand since lines/payment type may change.</summary>
public sealed record UpdateSalesInvoiceCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required SalesInvoicePaymentType PaymentType { get; init; }
    public bool CreditLimitOverrideApproved { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal? DiscountAmount { get; init; }
    public required IReadOnlyList<SalesInvoiceLineInput> Lines { get; init; }
}

public sealed class UpdateSalesInvoiceCommandValidator : AbstractValidator<UpdateSalesInvoiceCommand>
{
    public UpdateSalesInvoiceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
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

public sealed class UpdateSalesInvoiceCommandHandler(IApplicationDbContext db, IUserAccessService access) : IRequestHandler<UpdateSalesInvoiceCommand>
{
    public async Task Handle(UpdateSalesInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.SalesInvoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesInvoice), request.Id);

        if (invoice.Status != SalesInvoiceStatus.Draft)
        {
            throw new BusinessRuleException("SALES-INVOICE-NOT-DRAFT", "لا يمكن تعديل فاتورة المبيعات إلا وهي في حالة مسودة.");
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        // A role that may not edit the discount on this screen keeps the stored ones — the form
        // received them hidden (null). A line keeps the discount of the stored line of the same item.
        var discountEditable = await access.HasFieldPermissionOnScreenAsync(null, "SalesInvoice", "DiscountAmount", FieldAction.Edit, cancellationToken);
        var discount = discountEditable ? request.DiscountAmount : invoice.DiscountAmount;
        var stored = invoice.Lines.OrderBy(l => l.LineNumber).ToList();
        var inputs = discountEditable
            ? request.Lines
            : request.Lines.Select(l => l with { DiscountAmount = stored.FirstOrDefault(s => s.ItemId == l.ItemId)?.DiscountAmount }).ToList();
        var (lines, subtotal) = SalesInvoiceLineBuilder.Build(inputs);
        var totalAmount = subtotal - (discount ?? 0) + request.TaxAmount;

        if (request.PaymentType == SalesInvoicePaymentType.Credit)
        {
            var outstandingBalance = await db.SalesInvoices
                .Where(i => i.CustomerId == request.CustomerId && i.Status == SalesInvoiceStatus.Posted && i.Id != request.Id)
                .SumAsync(i => i.TotalAmount - i.AmountPaid, cancellationToken);

            if (outstandingBalance + totalAmount > customer.CreditLimit && !request.CreditLimitOverrideApproved)
            {
                throw new BusinessRuleException(
                    "SALES-INVOICE-CREDIT-LIMIT-EXCEEDED",
                    $"تجاوز حد الائتمان — الرصيد المستحق الحالي {outstandingBalance:F2} + قيمة الفاتورة {totalAmount:F2} يتجاوز حد ائتمان العميل {customer.CreditLimit:F2}.");
            }
        }

        db.Entry(invoice).Property(nameof(SalesInvoice.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        invoice.BranchId = request.BranchId;
        invoice.CustomerId = request.CustomerId;
        invoice.InvoiceDate = request.InvoiceDate;
        invoice.PaymentType = request.PaymentType;
        invoice.CreditLimitOverrideApproved = request.PaymentType == SalesInvoicePaymentType.Credit && request.CreditLimitOverrideApproved;
        invoice.TaxAmount = request.TaxAmount;
        invoice.DiscountAmount = discount;

        db.SalesInvoiceLines.RemoveRange(invoice.Lines);
        invoice.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);

        foreach (var line in lines)
        {
            invoice.Lines.Add(line);
        }

        invoice.Subtotal = subtotal;
        invoice.TotalAmount = totalAmount;

        await db.SaveChangesAsync(cancellationToken);
    }
}
