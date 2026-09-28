using Habbak.ERP.Application.Accounting.Periods.Common;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;

namespace Habbak.ERP.Application.Accounting.Periods.Commands.ClosePeriod;

/// <summary>Rule 8: every checklist item must be satisfied, re-verified live at the moment of closing.</summary>
public sealed record ClosePeriodCommand(long Id) : IRequest;

public sealed class ClosePeriodCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<ClosePeriodCommand>
{
    public async Task Handle(ClosePeriodCommand request, CancellationToken cancellationToken)
    {
        var period = await db.AccountingPeriods.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(AccountingPeriod), request.Id);

        if (period.Status != AccountingPeriodStatus.Open)
        {
            throw new BusinessRuleException("ACC-PERIOD-NOT-OPEN", "الفترة مقفلة بالفعل.");
        }

        var checklist = await PeriodChecklistEvaluator.EvaluateAsync(db, period, cancellationToken);
        if (checklist.Any(c => !c.IsSatisfied))
        {
            throw new BusinessRuleException(
                "ACC-R8-CHECKLIST-INCOMPLETE", "لا يمكن إقفال الفترة قبل استيفاء كل بنود الـ Checklist.");
        }

        period.Status = AccountingPeriodStatus.Closed;
        period.ClosedByUserId = currentCompanyContext.UserId;
        period.ClosedAtUtc = DateTime.UtcNow;

        foreach (var item in checklist)
        {
            db.PeriodCloseChecklistItems.Add(new PeriodCloseChecklistItem
            {
                PeriodId = period.Id,
                ItemKey = Enum.Parse<PeriodCloseChecklistItemKey>(item.ItemKey),
                IsSatisfied = item.IsSatisfied,
                CheckedAtUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
