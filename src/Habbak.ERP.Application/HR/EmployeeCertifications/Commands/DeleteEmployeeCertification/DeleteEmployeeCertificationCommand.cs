using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeCertifications.Commands.DeleteEmployeeCertification;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteEmployeeCertificationCommand(long Id) : IRequest;

public sealed class DeleteEmployeeCertificationCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteEmployeeCertificationCommand>
{
    public async Task Handle(DeleteEmployeeCertificationCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.EmployeeCertifications.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeCertification), request.Id);

        db.EmployeeCertifications.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
