using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Warehouses.Commands.DeleteWarehouse;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteWarehouseCommand(long Id) : IRequest;

public sealed class DeleteWarehouseCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteWarehouseCommand>
{
    public async Task Handle(DeleteWarehouseCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Warehouses.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.Id);

        db.Warehouses.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
