using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.Suppliers.Commands.DeleteSupplier;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteSupplierCommand(long Id) : IRequest;

public sealed class DeleteSupplierCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteSupplierCommand>
{
    public async Task Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Suppliers.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), request.Id);

        db.Suppliers.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
