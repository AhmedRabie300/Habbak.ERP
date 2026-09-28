using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Accounting.AccountOpeningBalances.Dtos;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.AccountOpeningBalances.Commands.UpdateAccountOpeningBalanceBatch;

/// <summary>Edits a Draft batch — locked once Posted (the journal entry it creates is
/// irreversible, same as every other posted document in this codebase).</summary>
public sealed record UpdateAccountOpeningBalanceBatchCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public required DateOnly TransactionDate { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<AccountOpeningBalanceLineInput> Lines { get; init; }
}

public sealed class UpdateAccountOpeningBalanceBatchCommandValidator : AbstractValidator<UpdateAccountOpeningBalanceBatchCommand>
{
    public UpdateAccountOpeningBalanceBatchCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.TransactionDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("قيد الأرصدة الافتتاحية يحتاج حساب واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.AccountId).GreaterThan(0);
            line.RuleFor(l => l.Amount).GreaterThan(0);
        });
    }
}

public sealed class UpdateAccountOpeningBalanceBatchCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateAccountOpeningBalanceBatchCommand>
{
    public async Task Handle(UpdateAccountOpeningBalanceBatchCommand request, CancellationToken cancellationToken)
    {
        var batch = await db.AccountOpeningBalanceBatches
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AccountOpeningBalanceBatch), request.Id);

        if (batch.Status != AccountOpeningBalanceStatus.Draft)
        {
            throw new BusinessRuleException("ACC-OPENING-BALANCE-NOT-EDITABLE", "لا يمكن تعديل قيد الأرصدة الافتتاحية إلا وهو في حالة مسودة.");
        }

        var accountIds = request.Lines.Select(l => l.AccountId).Distinct().ToList();
        var postableCount = await db.Accounts.CountAsync(a => accountIds.Contains(a.Id) && a.IsPostable, cancellationToken);
        if (postableCount != accountIds.Count)
        {
            throw new BusinessRuleException("ACC-OPENING-BALANCE-NOT-POSTABLE", "لا يمكن إدخال رصيد افتتاحي إلا على حساب قابل للترحيل.");
        }

        db.Entry(batch).Property(nameof(AccountOpeningBalanceBatch.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        batch.TransactionDate = request.TransactionDate;
        batch.Notes = request.Notes;

        // Two round trips: replacement lines reuse LineNumber 1, 2, 3... and the unique
        // (AccountOpeningBalanceBatchId, LineNumber) index is checked per-statement.
        db.AccountOpeningBalanceLines.RemoveRange(batch.Lines);
        batch.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);

        var lineNumber = 1;
        foreach (var line in request.Lines)
        {
            batch.Lines.Add(new AccountOpeningBalanceLine { LineNumber = lineNumber++, AccountId = line.AccountId, Amount = line.Amount, Notes = line.Notes });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
