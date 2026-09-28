using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;

namespace Habbak.ERP.Application.Accounting.Periods.Commands.ReopenPeriod;

/// <summary>
/// Rule 10: exceptional action, requires a higher permission than closing itself, and must
/// always be audited regardless of the entity's general audit setting — permission enforcement
/// and the mandatory-audit override both belong to the not-yet-built Settings/Audit
/// infrastructure; this handler only performs the state transition.
/// </summary>
public sealed record ReopenPeriodCommand(long Id) : IRequest;

public sealed class ReopenPeriodCommandHandler(IApplicationDbContext db) : IRequestHandler<ReopenPeriodCommand>
{
    public async Task Handle(ReopenPeriodCommand request, CancellationToken cancellationToken)
    {
        var period = await db.AccountingPeriods.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(AccountingPeriod), request.Id);

        if (period.Status != AccountingPeriodStatus.Closed)
        {
            throw new BusinessRuleException("ACC-PERIOD-NOT-CLOSED", "الفترة مفتوحة بالفعل.");
        }

        period.Status = AccountingPeriodStatus.Open;
        period.ClosedByUserId = null;
        period.ClosedAtUtc = null;

        await db.SaveChangesAsync(cancellationToken);
    }
}
