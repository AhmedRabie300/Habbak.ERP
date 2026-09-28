using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.DrawerMovements.Commands.ApproveDrawerMovement;

public sealed record ApproveDrawerMovementCommand(long Id) : IRequest;

public sealed class ApproveDrawerMovementCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<ApproveDrawerMovementCommand>
{
    public async Task Handle(ApproveDrawerMovementCommand request, CancellationToken cancellationToken)
    {
        var movement = await db.DrawerMovements.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DrawerMovement), request.Id);

        if (movement.Status != DrawerMovementStatus.Recorded)
        {
            throw new BusinessRuleException("POS-DRAWER-MOVEMENT-NOT-RECORDED", "لا يمكن اعتماد حركة درج إلا وهي في حالة مسجَّلة.");
        }

        movement.Status = DrawerMovementStatus.Approved;
        movement.ApprovedByUserId = currentCompanyContext.UserId;
        movement.ApprovedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}
