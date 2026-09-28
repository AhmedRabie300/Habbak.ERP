using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.AccountOpeningBalances.Commands.CancelAccountOpeningBalanceBatch;

/// <summary>Draft → Cancelled — no posting after this, matching rule 19-style "no cancel after posting" pattern.</summary>
public sealed record CancelAccountOpeningBalanceBatchCommand(long Id) : IRequest;

public sealed class CancelAccountOpeningBalanceBatchCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelAccountOpeningBalanceBatchCommand>
{
    public async Task Handle(CancelAccountOpeningBalanceBatchCommand request, CancellationToken cancellationToken)
    {
        var batch = await db.AccountOpeningBalanceBatches.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AccountOpeningBalanceBatch), request.Id);

        if (batch.Status != AccountOpeningBalanceStatus.Draft)
        {
            throw new BusinessRuleException("ACC-OPENING-BALANCE-NOT-CANCELLABLE", "لا يمكن إلغاء قيد الأرصدة الافتتاحية إلا وهو في حالة مسودة.");
        }

        batch.Status = AccountOpeningBalanceStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
    }
}
