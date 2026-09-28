using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.ItemGroups.Commands.DeleteItemGroup;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteItemGroupCommand(long Id) : IRequest;

public sealed class DeleteItemGroupCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteItemGroupCommand>
{
    public async Task Handle(DeleteItemGroupCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.ItemGroups.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ItemGroup), request.Id);

        db.ItemGroups.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
