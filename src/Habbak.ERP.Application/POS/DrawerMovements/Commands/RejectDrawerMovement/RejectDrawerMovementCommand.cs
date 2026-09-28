using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.DrawerMovements.Commands.RejectDrawerMovement;

public sealed record RejectDrawerMovementCommand(long Id) : IRequest;

public sealed class RejectDrawerMovementCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<RejectDrawerMovementCommand>
{
    public async Task Handle(RejectDrawerMovementCommand request, CancellationToken cancellationToken)
    {
        var movement = await db.DrawerMovements.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DrawerMovement), request.Id);

        if (movement.Status != DrawerMovementStatus.Recorded)
        {
            throw new BusinessRuleException("POS-DRAWER-MOVEMENT-NOT-RECORDED", "لا يمكن رفض حركة درج إلا وهي في حالة مسجَّلة.");
        }

        movement.Status = DrawerMovementStatus.Rejected;
        movement.ApprovedByUserId = currentCompanyContext.UserId;
        movement.ApprovedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}
