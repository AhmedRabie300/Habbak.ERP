using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.SupplierPayments;

/// <summary>
/// Spreading one supplier payment across the invoices it settles (Remarks4, item 7).
///
/// Before this, a payment could name a single invoice (Voucher.RelatedInvoiceId) — so paying a
/// supplier for four invoices with one transfer meant four vouchers, or one voucher that quietly
/// overpaid one invoice. The allocation rows below are written while the payment is still a Draft
/// and applied to PurchaseInvoice.AmountPaid when it is posted.
/// </summary>
public sealed record SupplierPaymentAllocationInput(long PurchaseInvoiceId, decimal Amount);

public sealed record SupplierPaymentAllocationDto
{
    public required long PurchaseInvoiceId { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required DateOnly DueDate { get; init; }
    public required decimal InvoiceTotal { get; init; }
    public required decimal AmountPaid { get; init; }
    public required decimal Amount { get; init; }
}

public sealed record GetSupplierPaymentAllocationsQuery(long VoucherId) : IRequest<IReadOnlyList<SupplierPaymentAllocationDto>>;

public sealed class GetSupplierPaymentAllocationsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSupplierPaymentAllocationsQuery, IReadOnlyList<SupplierPaymentAllocationDto>>
{
    public async Task<IReadOnlyList<SupplierPaymentAllocationDto>> Handle(
        GetSupplierPaymentAllocationsQuery request, CancellationToken cancellationToken) =>
        await db.SupplierPaymentAllocations.AsNoTracking()
            .Where(a => a.VoucherId == request.VoucherId)
            .OrderBy(a => a.PurchaseInvoice!.DueDate)
            .Select(a => new SupplierPaymentAllocationDto
            {
                PurchaseInvoiceId = a.PurchaseInvoiceId,
                InvoiceNumber = a.PurchaseInvoice!.InvoiceNumber,
                InvoiceDate = a.PurchaseInvoice!.InvoiceDate,
                DueDate = a.PurchaseInvoice!.DueDate,
                InvoiceTotal = a.PurchaseInvoice!.TotalAmount,
                AmountPaid = a.PurchaseInvoice!.AmountPaid,
                Amount = a.Amount
            })
            .ToListAsync(cancellationToken);
}

/// <summary>
/// Replaces the whole allocation set of a Draft payment. Sending an empty list clears it, which
/// leaves a payment on account — legitimate when the supplier has not invoiced yet.
/// </summary>
public sealed record AllocateSupplierPaymentCommand(long VoucherId, IReadOnlyList<SupplierPaymentAllocationInput> Allocations) : IRequest;

public sealed class AllocateSupplierPaymentCommandValidator : AbstractValidator<AllocateSupplierPaymentCommand>
{
    public AllocateSupplierPaymentCommandValidator()
    {
        RuleFor(x => x.VoucherId).GreaterThan(0);
        RuleForEach(x => x.Allocations).ChildRules(a =>
        {
            a.RuleFor(x => x.PurchaseInvoiceId).GreaterThan(0);
            a.RuleFor(x => x.Amount).GreaterThan(0).WithMessage("مبلغ التوزيع لازم يكون أكبر من صفر.");
        });
    }
}

public sealed class AllocateSupplierPaymentCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<AllocateSupplierPaymentCommand>
{
    public async Task Handle(AllocateSupplierPaymentCommand request, CancellationToken cancellationToken)
    {
        var voucher = await db.Vouchers.FirstOrDefaultAsync(v => v.Id == request.VoucherId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Voucher), request.VoucherId);

        if (voucher.VoucherType != VoucherType.Payment || voucher.CounterpartyType != CounterpartyType.Supplier)
        {
            throw new BusinessRuleException("PUR-PAYMENT-NOT-SUPPLIER", "التوزيع ده لسداد الموردين بس.");
        }

        if (voucher.Status != VoucherStatus.Draft)
        {
            throw new BusinessRuleException("PUR-PAYMENT-NOT-DRAFT", "مينفعش تعدّل توزيع سداد مترحّل — الغيه الأول.");
        }

        var existing = await db.SupplierPaymentAllocations.Where(a => a.VoucherId == voucher.Id).ToListAsync(cancellationToken);
        db.SupplierPaymentAllocations.RemoveRange(existing);

        if (request.Allocations.Count == 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (request.Allocations.Select(a => a.PurchaseInvoiceId).Distinct().Count() != request.Allocations.Count)
        {
            throw new BusinessRuleException("PUR-PAYMENT-ALLOCATION-DUPLICATE", "نفس الفاتورة مكرّرة في التوزيع.");
        }

        var invoiceIds = request.Allocations.Select(a => a.PurchaseInvoiceId).ToList();
        var invoices = await db.PurchaseInvoices
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        foreach (var allocation in request.Allocations)
        {
            if (!invoices.TryGetValue(allocation.PurchaseInvoiceId, out var invoice))
            {
                throw new NotFoundException(nameof(PurchaseInvoice), allocation.PurchaseInvoiceId);
            }

            if (invoice.SupplierId != voucher.CounterpartyId)
            {
                throw new BusinessRuleException(
                    "PUR-PAYMENT-INVOICE-SUPPLIER-MISMATCH", $"الفاتورة {invoice.InvoiceNumber} لمورد تاني.");
            }

            if (!string.Equals(invoice.CurrencyCode, voucher.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException(
                    "PUR-PAYMENT-CURRENCY-MISMATCH", $"عملة الفاتورة {invoice.InvoiceNumber} مختلفة عن عملة السداد.");
            }

            if (invoice.Status is not (PurchaseInvoiceStatus.Posted or PurchaseInvoiceStatus.PartiallyPaid))
            {
                throw new BusinessRuleException(
                    "PUR-PAYMENT-INVOICE-NOT-PAYABLE", $"الفاتورة {invoice.InvoiceNumber} مش قابلة للسداد في حالتها الحالية.");
            }

            var outstanding = invoice.TotalAmount - invoice.AmountPaid;
            if (allocation.Amount > outstanding)
            {
                throw new BusinessRuleException(
                    "PUR-PAYMENT-EXCEEDS-OUTSTANDING",
                    $"مبلغ التوزيع على الفاتورة {invoice.InvoiceNumber} ({allocation.Amount:N2}) أكبر من المتبقي عليها ({outstanding:N2}).");
            }

            db.SupplierPaymentAllocations.Add(new SupplierPaymentAllocation
            {
                CompanyId = current.CompanyId,
                VoucherId = voucher.Id,
                PurchaseInvoiceId = invoice.Id,
                Amount = allocation.Amount
            });
        }

        var total = request.Allocations.Sum(a => a.Amount);
        if (total != voucher.Amount)
        {
            throw new BusinessRuleException(
                "PUR-PAYMENT-ALLOCATION-MISMATCH",
                $"مجموع التوزيع ({total:N2}) لازم يساوي قيمة السداد ({voucher.Amount:N2}).");
        }

        // The single-invoice field stays in step for a one-invoice payment, so anything still
        // reading it (older vouchers, reports) sees the same answer.
        voucher.RelatedInvoiceId = request.Allocations.Count == 1 ? request.Allocations[0].PurchaseInvoiceId : null;

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Spreads an amount over the supplier's outstanding invoices, oldest due date first — what the
/// payment screen proposes before the user edits it. Pure calculation: it writes nothing.
/// </summary>
public sealed record ProposeSupplierPaymentAllocationQuery(long SupplierId, decimal Amount, string CurrencyCode)
    : IRequest<IReadOnlyList<SupplierPaymentAllocationInput>>;

public sealed class ProposeSupplierPaymentAllocationQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ProposeSupplierPaymentAllocationQuery, IReadOnlyList<SupplierPaymentAllocationInput>>
{
    public async Task<IReadOnlyList<SupplierPaymentAllocationInput>> Handle(
        ProposeSupplierPaymentAllocationQuery request, CancellationToken cancellationToken)
    {
        var invoices = await db.PurchaseInvoices.AsNoTracking()
            .Where(i => i.SupplierId == request.SupplierId
                        && i.CurrencyCode == request.CurrencyCode
                        && (i.Status == PurchaseInvoiceStatus.Posted || i.Status == PurchaseInvoiceStatus.PartiallyPaid))
            .OrderBy(i => i.DueDate).ThenBy(i => i.Id)
            .Select(i => new { i.Id, Outstanding = i.TotalAmount - i.AmountPaid })
            .ToListAsync(cancellationToken);

        var remaining = request.Amount;
        var proposal = new List<SupplierPaymentAllocationInput>();
        foreach (var invoice in invoices.Where(i => i.Outstanding > 0))
        {
            if (remaining <= 0)
            {
                break;
            }

            var amount = Math.Min(remaining, invoice.Outstanding);
            proposal.Add(new SupplierPaymentAllocationInput(invoice.Id, amount));
            remaining -= amount;
        }

        return proposal;
    }
}
