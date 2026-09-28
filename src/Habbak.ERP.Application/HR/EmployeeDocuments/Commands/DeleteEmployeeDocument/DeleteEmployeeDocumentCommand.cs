using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeDocuments.Commands.DeleteEmployeeDocument;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteEmployeeDocumentCommand(long Id) : IRequest;

public sealed class DeleteEmployeeDocumentCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteEmployeeDocumentCommand>
{
    public async Task Handle(DeleteEmployeeDocumentCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.EmployeeDocuments.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeDocument), request.Id);

        db.EmployeeDocuments.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
