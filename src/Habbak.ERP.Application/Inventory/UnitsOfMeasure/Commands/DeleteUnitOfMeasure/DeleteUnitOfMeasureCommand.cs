using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.UnitsOfMeasure.Commands.DeleteUnitOfMeasure;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteUnitOfMeasureCommand(long Id) : IRequest;

public sealed class DeleteUnitOfMeasureCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteUnitOfMeasureCommand>
{
    public async Task Handle(DeleteUnitOfMeasureCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.UnitsOfMeasure.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(UnitOfMeasure), request.Id);

        db.UnitsOfMeasure.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
