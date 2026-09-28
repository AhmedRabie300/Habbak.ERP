using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Shared.Enums;
using MediatR;

namespace Habbak.ERP.Application.Accounting.Custody.Commands.CreateCustodySettlement;

public sealed record CustodySettlementLineInput(long AccountId, decimal Amount, string? Description);

/// <summary>
/// Settles a custody (rule 6: full distribution across accounts required — CustodySettlementLine
/// list here IS that distribution). Posts: Dr each line's account for its Amount, Cr the custody
/// receivable account for the FULL original custody Amount, and a balancing Treasury line for
/// the difference (cash returned if positive, additional cash paid if negative) — see the
/// handler's comment for the accounting identity that makes this always balance.
/// CustodyReceivableAccountId/TreasuryAccountId are supplied here again (not stored on
/// CustodyRegister) — same simplification noted on CreateCustodyRegisterCommand.
/// </summary>
public sealed record CreateCustodySettlementCommand(
    long CustodyRegisterId, DateOnly SettlementDate, long CustodyReceivableAccountId, long TreasuryAccountId,
    IReadOnlyList<CustodySettlementLineInput> Lines) : IRequest<long>;

public sealed class CreateCustodySettlementCommandValidator : AbstractValidator<CreateCustodySettlementCommand>
{
    public CreateCustodySettlementCommandValidator()
    {
        RuleFor(x => x.CustodyRegisterId).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("لازم بند واحد على الأقل لتوزيع مصروفات العهدة.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.AccountId).GreaterThan(0);
            line.RuleFor(l => l.Amount).GreaterThan(0);
        });
    }
}

public sealed class CreateCustodySettlementCommandHandler(IApplicationDbContext db, IPostingService postingService)
    : IRequestHandler<CreateCustodySettlementCommand, long>
{
    public async Task<long> Handle(CreateCustodySettlementCommand request, CancellationToken cancellationToken)
    {
        var custody = await db.CustodyRegisters.FindAsync([request.CustodyRegisterId], cancellationToken)
            ?? throw new NotFoundException(nameof(CustodyRegister), request.CustodyRegisterId);

        if (custody.Status is not (CustodyStatus.Open or CustodyStatus.PartiallySettled))
        {
            throw new BusinessRuleException("ACC-CUSTODY-NOT-OPEN", "لا يمكن تسوية عهدة مغلقة أو ملغاة.");
        }

        var totalSpent = request.Lines.Sum(l => l.Amount);

        // Rule 22: total settlement lines cannot exceed the custody's original amount.
        if (totalSpent > custody.Amount)
        {
            throw new BusinessRuleException(
                "ACC-R22-SETTLEMENT-EXCEEDS-CUSTODY", "إجمالي بنود التسوية لا يجوز أن يتجاوز قيمة العهدة الأصلية.");
        }

        // Positive = employee returns unspent cash, negative = company owes the employee more.
        var remainingAmount = custody.Amount - totalSpent;

        var lines = new List<PostingLineRequest>();
        foreach (var line in request.Lines)
        {
            lines.Add(new PostingLineRequest
            {
                AccountId = line.AccountId, DebitAmount = line.Amount, CreditAmount = 0m,
                CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = line.Amount, BaseCurrencyCreditAmount = 0m,
                Description = line.Description
            });
        }

        // Clear the FULL original receivable...
        lines.Add(new PostingLineRequest
        {
            AccountId = request.CustodyReceivableAccountId, DebitAmount = 0m, CreditAmount = custody.Amount,
            CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = 0m, BaseCurrencyCreditAmount = custody.Amount
        });

        // ...then balance the difference through the treasury (identity: TotalDebit == TotalCredit
        // holds whether remainingAmount is positive, negative, or zero — see class XML doc).
        if (remainingAmount > 0)
        {
            lines.Add(new PostingLineRequest
            {
                AccountId = request.TreasuryAccountId, DebitAmount = remainingAmount, CreditAmount = 0m,
                CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = remainingAmount, BaseCurrencyCreditAmount = 0m
            });
        }
        else if (remainingAmount < 0)
        {
            lines.Add(new PostingLineRequest
            {
                AccountId = request.TreasuryAccountId, DebitAmount = 0m, CreditAmount = -remainingAmount,
                CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = 0m, BaseCurrencyCreditAmount = -remainingAmount
            });
        }

        var postingRequest = new PostingRequest
        {
            CompanyId = custody.CompanyId!.Value,
            BranchId = custody.BranchId,
            EntryDate = request.SettlementDate,
            Description = $"تسوية عهدة رقم {custody.Id}",
            SourceModule = SourceModule.Manual,
            IsAutoGenerated = true,
            Lines = lines
        };

        var result = await postingService.PostAsync(postingRequest, cancellationToken);

        var settlement = new CustodySettlement
        {
            CustodyRegisterId = custody.Id,
            SettlementDate = request.SettlementDate,
            RemainingAmount = remainingAmount,
            JournalEntry = result.JournalEntry
        };

        foreach (var line in request.Lines)
        {
            settlement.Lines.Add(new CustodySettlementLine
            {
                AccountId = line.AccountId,
                Amount = line.Amount,
                Description = line.Description
            });
        }

        // This settlement always clears the custody's full original Amount in one shot (via the
        // treasury balancing line above), so the register always ends up fully Closed here.
        // True incremental multi-settlement (leaving a portion PartiallySettled for a later
        // settlement to close) is a further simplification not implemented in this pass.
        custody.Status = CustodyStatus.Closed;

        db.CustodySettlements.Add(settlement);
        await db.SaveChangesAsync(cancellationToken);

        return settlement.Id;
    }
}
