using FluentValidation;
using Habbak.ERP.Application.Common;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Vouchers.Commands.UpdateVoucher;

/// <summary>Edits a Draft voucher (section 4.2 — only a Draft voucher is editable).</summary>
public sealed record UpdateVoucherCommand : IRequest
{
    public required long Id { get; init; }
    public required string RowVersion { get; init; }
    public long? BranchId { get; init; }
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

public sealed class UpdateVoucherCommandValidator : AbstractValidator<UpdateVoucherCommand>
{
    public UpdateVoucherCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.TreasuryAccountId).GreaterThan(0);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.ExchangeRate).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("لا يُسمح بإنشاء سند بمبلغ صفر.");
        RuleFor(x => x.Description).MaximumLength(500);

        RuleFor(x => x.CounterpartyId)
            .NotNull()
            .When(x => x.CounterpartyType is CounterpartyType.Customer or CounterpartyType.Supplier or CounterpartyType.Employee)
            .WithMessage("لازم تحديد الطرف (عميل/مورد/موظف) لهذا النوع.");

        RuleFor(x => x.DirectAccountId)
            .NotNull()
            .When(x => x.CounterpartyType == CounterpartyType.Other)
            .WithMessage("لازم تحديد حساب مباشر عند اختيار نوع طرف \"أخرى\".");
    }
}

public sealed class UpdateVoucherCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdateVoucherCommand>
{
    public async Task Handle(UpdateVoucherCommand request, CancellationToken cancellationToken)
    {
        var voucher = await db.Vouchers.FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Voucher), request.Id);

        if (voucher.Status != VoucherStatus.Draft)
        {
            throw new BusinessRuleException("ACC-VOUCHER-NOT-DRAFT", "لا يمكن تعديل سند إلا وهو في حالة مسودة.");
        }

        db.Entry(voucher).Property(nameof(Voucher.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        voucher.BranchId = BranchRequirement.Resolve(request.BranchId, currentCompanyContext.BranchId);
        voucher.VoucherDate = request.VoucherDate;
        voucher.TreasuryAccountId = request.TreasuryAccountId;
        voucher.Description = request.Description;
        voucher.CounterpartyType = request.CounterpartyType;
        voucher.CounterpartyId = request.CounterpartyId;
        voucher.DirectAccountId = request.DirectAccountId;
        voucher.Amount = request.Amount;
        voucher.CurrencyCode = request.CurrencyCode;
        voucher.ExchangeRate = request.ExchangeRate;
        voucher.BaseCurrencyAmount = request.BaseCurrencyAmount;
        voucher.RelatedInvoiceId = request.RelatedInvoiceId;

        await db.SaveChangesAsync(cancellationToken);
    }
}
