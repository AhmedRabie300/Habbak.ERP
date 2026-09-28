using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Settings.Users;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.ShiftAssignments.Commands.CreateShiftAssignment;

public sealed record CreateShiftAssignmentCommand(long POSTerminalId, long UserId, DateOnly AssignedDate) : IRequest<long>;

public sealed class CreateShiftAssignmentCommandValidator : AbstractValidator<CreateShiftAssignmentCommand>
{
    public CreateShiftAssignmentCommandValidator()
    {
        RuleFor(x => x.POSTerminalId).GreaterThan(0);
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.AssignedDate).NotEqual(default(DateOnly));
    }
}

public sealed class CreateShiftAssignmentCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<CreateShiftAssignmentCommand, long>
{
    public async Task<long> Handle(CreateShiftAssignmentCommand request, CancellationToken cancellationToken)
    {
        var terminal = await db.POSTerminals.FirstOrDefaultAsync(t => t.Id == request.POSTerminalId, cancellationToken)
            ?? throw new NotFoundException(nameof(POSTerminal), request.POSTerminalId);

        await CompanyUsers.EnsureWorksHereAsync(db, request.UserId, currentCompanyContext.CompanyId, cancellationToken);

        var exists = await db.ShiftAssignments.AnyAsync(
            a => a.POSTerminalId == request.POSTerminalId && a.UserId == request.UserId && a.AssignedDate == request.AssignedDate,
            cancellationToken);
        if (exists)
        {
            throw new BusinessRuleException("POS-SHIFT-ASSIGNMENT-EXISTS", "هذا المستخدم مُعيَّن بالفعل على هذا الجهاز في نفس اليوم.");
        }

        var assignment = new ShiftAssignment
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = terminal.BranchId,
            POSTerminalId = request.POSTerminalId,
            UserId = request.UserId,
            AssignedDate = request.AssignedDate
        };

        db.ShiftAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);

        return assignment.Id;
    }
}
