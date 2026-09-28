using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.ShiftAssignments.Commands.DeleteShiftAssignment;

public sealed record DeleteShiftAssignmentCommand(long Id) : IRequest;

public sealed class DeleteShiftAssignmentCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteShiftAssignmentCommand>
{
    public async Task Handle(DeleteShiftAssignmentCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.ShiftAssignments.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ShiftAssignment), request.Id);

        db.ShiftAssignments.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
