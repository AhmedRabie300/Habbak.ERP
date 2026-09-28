using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.JobPositions.Commands.DeleteJobPosition;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteJobPositionCommand(long Id) : IRequest;

public sealed class DeleteJobPositionCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteJobPositionCommand>
{
    public async Task Handle(DeleteJobPositionCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.JobPositions.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(JobPosition), request.Id);

        db.JobPositions.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
