using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeDocumentTypes.Commands.DeleteEmployeeDocumentType;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteEmployeeDocumentTypeCommand(long Id) : IRequest;

public sealed class DeleteEmployeeDocumentTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteEmployeeDocumentTypeCommand>
{
    public async Task Handle(DeleteEmployeeDocumentTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.EmployeeDocumentTypes.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeDocumentType), request.Id);

        db.EmployeeDocumentTypes.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
