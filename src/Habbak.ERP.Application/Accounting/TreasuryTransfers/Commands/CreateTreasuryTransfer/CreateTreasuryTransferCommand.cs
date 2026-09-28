using FluentValidation;
using Habbak.ERP.Application.Common;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.TreasuryTransfers.Commands.CreateTreasuryTransfer;

/// <summary>Creates a treasury/bank transfer as Draft (00-Frontend-Specs.md, section 7 — Create).</summary>
public sealed record CreateTreasuryTransferCommand : IRequest<long>
{
    public long? BranchId { get; init; }
    public required long FromTreasuryAccountId { get; init; }
    public required long ToTreasuryAccountId { get; init; }
    public required decimal Amount { get; init; }
    public required DateOnly TransferDate { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateTreasuryTransferCommandValidator : AbstractValidator<CreateTreasuryTransferCommand>
{
    public CreateTreasuryTransferCommandValidator()
    {
        RuleFor(x => x.FromTreasuryAccountId).GreaterThan(0);
        RuleFor(x => x.ToTreasuryAccountId).GreaterThan(0);

        // Rule 17 (01-Module-Accounting.md): no zero-amount receipt/payment/transfer.
        RuleFor(x => x.Amount).GreaterThan(0)
            .WithMessage("لا يُسمح بإنشاء تحويل بمبلغ صفر.");

        RuleFor(x => x)
            .Must(x => x.FromTreasuryAccountId != x.ToTreasuryAccountId)
            .WithMessage("لا يمكن التحويل من وإلى نفس الحساب.")
            .OverridePropertyName(nameof(CreateTreasuryTransferCommand.ToTreasuryAccountId));
    }
}

public sealed class CreateTreasuryTransferCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<CreateTreasuryTransferCommand, long>
{
    public async Task<long> Handle(CreateTreasuryTransferCommand request, CancellationToken cancellationToken)
    {
        var fromAccount = await db.Accounts.FirstOrDefaultAsync(a => a.Id == request.FromTreasuryAccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.FromTreasuryAccountId);
        var toAccount = await db.Accounts.FirstOrDefaultAsync(a => a.Id == request.ToTreasuryAccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.ToTreasuryAccountId);

        // Both accounts share one Amount column with no per-side currency/exchange-rate fields
        // (unlike Voucher), so a transfer only makes sense between two accounts of the same currency.
        if (fromAccount.CurrencyCode != toAccount.CurrencyCode)
        {
            throw new BusinessRuleException("ACC-TRANSFER-CURRENCY-MISMATCH", "لا يُسمح بالتحويل بين حسابين بعملتين مختلفتين.");
        }

        var transfer = new TreasuryTransfer
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = BranchRequirement.Resolve(request.BranchId, currentCompanyContext.BranchId),
            FromTreasuryAccountId = request.FromTreasuryAccountId,
            ToTreasuryAccountId = request.ToTreasuryAccountId,
            Amount = request.Amount,
            TransferDate = request.TransferDate,
            Notes = request.Notes,
            Status = TreasuryTransferStatus.Draft
        };

        db.TreasuryTransfers.Add(transfer);
        await db.SaveChangesAsync(cancellationToken);

        return transfer.Id;
    }
}
