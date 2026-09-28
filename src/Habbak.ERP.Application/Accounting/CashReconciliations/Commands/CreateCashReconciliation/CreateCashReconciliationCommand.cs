using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.CashReconciliations.Commands.CreateCashReconciliation;

public sealed record DenominationInput(decimal DenominationValue, int Count);

/// <summary>
/// ExpectedBalance is computed live from posted journal lines against TreasuryAccountId up to
/// ReconciliationDate — not entered manually — matching real reconciliation practice (comparing
/// a physical count to the actual book balance).
/// </summary>
public sealed record CreateCashReconciliationCommand(
    long TreasuryAccountId, long? BranchId, DateOnly ReconciliationDate, decimal ActualBalance,
    string? DifferenceReason, IReadOnlyList<DenominationInput>? Denominations) : IRequest<long>;

public sealed class CreateCashReconciliationCommandValidator : AbstractValidator<CreateCashReconciliationCommand>
{
    public CreateCashReconciliationCommandValidator()
    {
        RuleFor(x => x.TreasuryAccountId).GreaterThan(0);
    }
}

public sealed class CreateCashReconciliationCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<CreateCashReconciliationCommand, long>
{
    public async Task<long> Handle(CreateCashReconciliationCommand request, CancellationToken cancellationToken)
    {
        var expectedBalance = await db.JournalEntryLines
            .Where(l => l.AccountId == request.TreasuryAccountId
                && l.JournalEntry.Status == JournalEntryStatus.Posted
                && l.JournalEntry.EntryDate <= request.ReconciliationDate)
            .SumAsync(l => l.DebitAmount - l.CreditAmount, cancellationToken);

        var differenceAmount = request.ActualBalance - expectedBalance;

        // Field table, section 2.5: DifferenceReason is required whenever there IS a difference.
        if (differenceAmount != 0 && string.IsNullOrWhiteSpace(request.DifferenceReason))
        {
            throw new BusinessRuleException(
                "ACC-CASH-RECON-REASON-REQUIRED", "لازم تحديد سبب الفرق لأن الرصيد الفعلي مختلف عن الدفتري.");
        }

        var reconciliation = new CashReconciliation
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            TreasuryAccountId = request.TreasuryAccountId,
            ReconciliationDate = request.ReconciliationDate,
            ExpectedBalance = expectedBalance,
            ActualBalance = request.ActualBalance,
            DifferenceAmount = differenceAmount,
            DifferenceReason = request.DifferenceReason
        };

        foreach (var denomination in request.Denominations ?? [])
        {
            reconciliation.Denominations.Add(new CashReconciliationDenomination
            {
                DenominationValue = denomination.DenominationValue,
                Count = denomination.Count
            });
        }

        db.CashReconciliations.Add(reconciliation);
        await db.SaveChangesAsync(cancellationToken);

        return reconciliation.Id;
    }
}
