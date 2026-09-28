using FluentValidation;
using Habbak.ERP.Application.Common;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;

namespace Habbak.ERP.Application.Accounting.Vouchers.Commands.CreateVoucher;

/// <summary>Creates a receipt/payment voucher as Draft (00-Frontend-Specs.md, section 7 — Create).</summary>
public sealed record CreateVoucherCommand : IRequest<long>
{
    public long? BranchId { get; init; }
    public required VoucherType VoucherType { get; init; }
    public required DateOnly VoucherDate { get; init; }
    public required long TreasuryAccountId { get; init; }
    public string? Description { get; init; }
    public required CounterpartyType CounterpartyType { get; init; }
    public long? CounterpartyId { get; init; }
    public long? DirectAccountId { get; init; }
    public required decimal Amount { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal ExchangeRate { get; init; }
    public required decimal BaseCurrencyAmount { get; init; }
    public long? RelatedInvoiceId { get; init; }
}

public sealed class CreateVoucherCommandValidator : AbstractValidator<CreateVoucherCommand>
{
    public CreateVoucherCommandValidator()
    {
        RuleFor(x => x.TreasuryAccountId).GreaterThan(0);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.ExchangeRate).GreaterThan(0);
        RuleFor(x => x.Description).MaximumLength(500);

        // Rule 17: no zero-amount receipt/payment/transfer.
        RuleFor(x => x.Amount).GreaterThan(0)
            .WithMessage("لا يُسمح بإنشاء سند بمبلغ صفر.");

        // Field table, section 2.3: CounterpartyId required for Customer/Supplier/Employee.
        RuleFor(x => x.CounterpartyId)
            .NotNull()
            .When(x => x.CounterpartyType is CounterpartyType.Customer or CounterpartyType.Supplier or CounterpartyType.Employee)
            .WithMessage("لازم تحديد الطرف (عميل/مورد/موظف) لهذا النوع.");

        // Rule 28: DirectAccountId required only when CounterpartyType = Other.
        RuleFor(x => x.DirectAccountId)
            .NotNull()
            .When(x => x.CounterpartyType == CounterpartyType.Other)
            .WithMessage("لازم تحديد حساب مباشر عند اختيار نوع طرف \"أخرى\".");
    }
}

public sealed class CreateVoucherCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, IVoucherNumberGenerator numberGenerator)
    : IRequestHandler<CreateVoucherCommand, long>
{
    public async Task<long> Handle(CreateVoucherCommand request, CancellationToken cancellationToken)
    {
        var voucherNumber = await numberGenerator.GenerateAsync(
            currentCompanyContext.CompanyId, request.VoucherType, request.VoucherDate.Year, cancellationToken);

        var voucher = new Voucher
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = BranchRequirement.Resolve(request.BranchId, currentCompanyContext.BranchId),
            VoucherType = request.VoucherType,
            VoucherNumber = voucherNumber,
            VoucherDate = request.VoucherDate,
            TreasuryAccountId = request.TreasuryAccountId,
            Description = request.Description,
            CounterpartyType = request.CounterpartyType,
            CounterpartyId = request.CounterpartyId,
            DirectAccountId = request.DirectAccountId,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            ExchangeRate = request.ExchangeRate,
            BaseCurrencyAmount = request.BaseCurrencyAmount,
            RelatedInvoiceId = request.RelatedInvoiceId,
            Status = VoucherStatus.Draft
        };

        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync(cancellationToken);

        return voucher.Id;
    }
}
