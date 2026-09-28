using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.DrawerMovements.Commands.CreateDrawerMovement;

/// <summary>قاعدة 3: أي إيداع/سحب لازم يرتبط بوردية Open حاليًا. قاعدة 32: تُسجَّل Recorded ومالهاش
/// أي تأثير على حساب الفرق المتوقع لحد ما تتعتمد.</summary>
public sealed record CreateDrawerMovementCommand(long ShiftId, DrawerMovementType MovementType, decimal Amount, string? Reason) : IRequest<long>;

public sealed class CreateDrawerMovementCommandValidator : AbstractValidator<CreateDrawerMovementCommand>
{
    public CreateDrawerMovementCommandValidator()
    {
        RuleFor(x => x.ShiftId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class CreateDrawerMovementCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateDrawerMovementCommand, long>
{
    public async Task<long> Handle(CreateDrawerMovementCommand request, CancellationToken cancellationToken)
    {
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken)
            ?? throw new NotFoundException(nameof(Shift), request.ShiftId);

        if (shift.Status != ShiftStatus.Open)
        {
            throw new BusinessRuleException("POS-SHIFT-NOT-OPEN", "لا يمكن تسجيل حركة درج إلا على وردية مفتوحة حاليًا.");
        }

        var movement = new DrawerMovement
        {
            CompanyId = shift.CompanyId,
            BranchId = shift.BranchId,
            ShiftId = shift.Id,
            MovementType = request.MovementType,
            Amount = request.Amount,
            Reason = request.Reason,
            Status = DrawerMovementStatus.Recorded
        };

        db.DrawerMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);

        return movement.Id;
    }
}
