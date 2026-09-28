using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.CustodyOfficers.Commands.DeleteCustodyOfficer;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteCustodyOfficerCommand(long Id) : IRequest;

public sealed class DeleteCustodyOfficerCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteCustodyOfficerCommand>
{
    public async Task Handle(DeleteCustodyOfficerCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.CustodyOfficers.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CustodyOfficer), request.Id);

        db.CustodyOfficers.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
