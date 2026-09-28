using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Accounting.AccountOpeningBalances.Dtos;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.AccountOpeningBalances.Commands.CreateAccountOpeningBalanceBatch;

/// <summary>Creates an account opening-balance batch as Draft (My Remarks/Remarks2.md, bug 1.4 /
/// feature 3.9).</summary>
public sealed record CreateAccountOpeningBalanceBatchCommand : IRequest<long>
{
    public required DateOnly TransactionDate { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<AccountOpeningBalanceLineInput> Lines { get; init; }
}

public sealed class CreateAccountOpeningBalanceBatchCommandValidator : AbstractValidator<CreateAccountOpeningBalanceBatchCommand>
{
    public CreateAccountOpeningBalanceBatchCommandValidator()
    {
        RuleFor(x => x.TransactionDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("قيد الأرصدة الافتتاحية يحتاج حساب واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.AccountId).GreaterThan(0);
            line.RuleFor(l => l.Amount).GreaterThan(0);
        });
    }
}

public sealed class CreateAccountOpeningBalanceBatchCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateAccountOpeningBalanceBatchCommand, long>
{
    public async Task<long> Handle(CreateAccountOpeningBalanceBatchCommand request, CancellationToken cancellationToken)
    {
        var accountIds = request.Lines.Select(l => l.AccountId).Distinct().ToList();
        var postableCount = await db.Accounts.CountAsync(a => accountIds.Contains(a.Id) && a.IsPostable, cancellationToken);
        if (postableCount != accountIds.Count)
        {
            throw new BusinessRuleException("ACC-OPENING-BALANCE-NOT-POSTABLE", "لا يمكن إدخال رصيد افتتاحي إلا على حساب قابل للترحيل.");
        }

        var batchNumber = await codeGenerator.ResolveCodeAsync("ACCOUNTING_OPENING_BALANCES", null, cancellationToken);

        var batch = new AccountOpeningBalanceBatch
        {
            CompanyId = currentCompanyContext.CompanyId,
            BatchNumber = batchNumber,
            TransactionDate = request.TransactionDate,
            Status = AccountOpeningBalanceStatus.Draft,
            Notes = request.Notes
        };

        var lineNumber = 1;
        foreach (var line in request.Lines)
        {
            batch.Lines.Add(new AccountOpeningBalanceLine { LineNumber = lineNumber++, AccountId = line.AccountId, Amount = line.Amount, Notes = line.Notes });
        }

        db.AccountOpeningBalanceBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);

        return batch.Id;
    }
}
