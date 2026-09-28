using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;

namespace Habbak.ERP.Application.Accounting.BankReconciliations.Commands.AddBankReconciliationLine;

/// <summary>
/// Manual line entry — there is no bank-statement-import mechanism in this build (no dedicated
/// "imported statement line" table exists yet), so BankStatementLineId is whatever reference
/// number the user types in from the paper/PDF statement, not a real foreign key.
/// Enforces rule 23 at the Application layer too (not just the DB check constraint) so the user
/// gets a clean error instead of a raw SQL exception.
/// </summary>
public sealed record AddBankReconciliationLineCommand(
    long RunId, SystemTransactionType? SystemTransactionType, long? SystemTransactionId,
    long? BankStatementLineId, decimal MatchedAmount, bool IsAutoMatched) : IRequest<long>;

public sealed class AddBankReconciliationLineCommandHandler(IApplicationDbContext db)
    : IRequestHandler<AddBankReconciliationLineCommand, long>
{
    public async Task<long> Handle(AddBankReconciliationLineCommand request, CancellationToken cancellationToken)
    {
        var run = await db.BankReconciliationRuns.FindAsync([request.RunId], cancellationToken)
            ?? throw new NotFoundException(nameof(BankReconciliationRun), request.RunId);

        if (run.Status != BankReconciliationStatus.InProgress)
        {
            throw new BusinessRuleException("ACC-BANK-RECON-NOT-IN-PROGRESS", "لا يمكن إضافة سطور لمطابقة مكتملة.");
        }

        // Rule 23.
        if (request.SystemTransactionId is null && request.BankStatementLineId is null)
        {
            throw new BusinessRuleException(
                "ACC-R23-SOURCE-REQUIRED", "كل سطر مطابقة لازم يشير لمصدر واحد على الأقل (حركة نظام أو حركة كشف بنكي).");
        }

        if (request.SystemTransactionId is not null && request.SystemTransactionType is null)
        {
            throw new BusinessRuleException(
                "ACC-R23-TYPE-REQUIRED", "لازم تحديد نوع الحركة (SystemTransactionType) عند تحديد حركة نظام.");
        }

        var line = new BankReconciliationLine
        {
            BankReconciliationRunId = request.RunId,
            SystemTransactionType = request.SystemTransactionType,
            SystemTransactionId = request.SystemTransactionId,
            BankStatementLineId = request.BankStatementLineId,
            MatchedAmount = request.MatchedAmount,
            IsAutoMatched = request.IsAutoMatched
        };

        db.BankReconciliationLines.Add(line);
        await db.SaveChangesAsync(cancellationToken);

        return line.Id;
    }
}
