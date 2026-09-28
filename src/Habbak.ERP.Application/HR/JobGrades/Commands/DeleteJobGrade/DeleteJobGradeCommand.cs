using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.JobGrades.Commands.DeleteJobGrade;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteJobGradeCommand(long Id) : IRequest;

public sealed class DeleteJobGradeCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteJobGradeCommand>
{
    public async Task Handle(DeleteJobGradeCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.JobGrades.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(JobGrade), request.Id);

        db.JobGrades.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
