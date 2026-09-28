using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Dimensions.Commands.DeleteDimension;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button. DimensionsPage has no
/// Edit/List navigation of its own (a single dual-list screen), so this is wired to an inline
/// delete icon per row instead of an ActionBar destructive slot.</summary>
public sealed record DeleteDimensionCommand(long Id) : IRequest;

public sealed class DeleteDimensionCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteDimensionCommand>
{
    public async Task Handle(DeleteDimensionCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.CostCenterDimensions.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CostCenterDimension), request.Id);

        db.CostCenterDimensions.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
