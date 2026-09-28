using FluentValidation;
using Habbak.ERP.Application.Common;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.TreasuryTransfers.Commands.UpdateTreasuryTransfer;

public sealed record UpdateTreasuryTransferCommand : IRequest
{
    public required long Id { get; init; }
    public required string RowVersion { get; init; }
    public long? BranchId { get; init; }
    public required long FromTreasuryAccountId { get; init; }
    public required long ToTreasuryAccountId { get; init; }
    public required decimal Amount { get; init; }
    public required DateOnly TransferDate { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateTreasuryTransferCommandValidator : AbstractValidator<UpdateTreasuryTransferCommand>
{
    public UpdateTreasuryTransferCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FromTreasuryAccountId).GreaterThan(0);
        RuleFor(x => x.ToTreasuryAccountId).GreaterThan(0);

        RuleFor(x => x.Amount).GreaterThan(0)
            .WithMessage("لا يُسمح بإنشاء تحويل بمبلغ صفر.");

        RuleFor(x => x)
            .Must(x => x.FromTreasuryAccountId != x.ToTreasuryAccountId)
            .WithMessage("لا يمكن التحويل من وإلى نفس الحساب.")
            .OverridePropertyName(nameof(UpdateTreasuryTransferCommand.ToTreasuryAccountId));
    }
}

public sealed class UpdateTreasuryTransferCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdateTreasuryTransferCommand>
{
    public async Task Handle(UpdateTreasuryTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await db.TreasuryTransfers.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TreasuryTransfer), request.Id);

        if (transfer.Status != TreasuryTransferStatus.Draft)
        {
            throw new BusinessRuleException("ACC-TRANSFER-NOT-DRAFT", "لا يمكن تعديل تحويل إلا وهو في حالة مسودة.");
        }

        var fromAccount = await db.Accounts.FirstOrDefaultAsync(a => a.Id == request.FromTreasuryAccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.FromTreasuryAccountId);
        var toAccount = await db.Accounts.FirstOrDefaultAsync(a => a.Id == request.ToTreasuryAccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.ToTreasuryAccountId);

        if (fromAccount.CurrencyCode != toAccount.CurrencyCode)
        {
            throw new BusinessRuleException("ACC-TRANSFER-CURRENCY-MISMATCH", "لا يُسمح بالتحويل بين حسابين بعملتين مختلفتين.");
        }

        db.Entry(transfer).Property(t => t.RowVersion).OriginalValue = Convert.FromBase64String(request.RowVersion);

        transfer.BranchId = BranchRequirement.Resolve(request.BranchId, currentCompanyContext.BranchId);
        transfer.FromTreasuryAccountId = request.FromTreasuryAccountId;
        transfer.ToTreasuryAccountId = request.ToTreasuryAccountId;
        transfer.Amount = request.Amount;
        transfer.TransferDate = request.TransferDate;
        transfer.Notes = request.Notes;

        await db.SaveChangesAsync(cancellationToken);
    }
}
