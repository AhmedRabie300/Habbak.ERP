using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.POSCategories.Commands.DeletePOSCategory;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeletePOSCategoryCommand(long Id) : IRequest;

public sealed class DeletePOSCategoryCommandHandler(IApplicationDbContext db) : IRequestHandler<DeletePOSCategoryCommand>
{
    public async Task Handle(DeletePOSCategoryCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.POSCategories.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(POSCategory), request.Id);

        db.POSCategories.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
