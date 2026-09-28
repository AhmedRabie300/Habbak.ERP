using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Dimensions.Commands.DeleteDimensionValue;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button (see DeleteDimensionCommand's
/// note on this screen's own dual-list layout).</summary>
public sealed record DeleteDimensionValueCommand(long Id) : IRequest;

public sealed class DeleteDimensionValueCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteDimensionValueCommand>
{
    public async Task Handle(DeleteDimensionValueCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.CostCenterDimensionValues.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CostCenterDimensionValue), request.Id);

        db.CostCenterDimensionValues.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
