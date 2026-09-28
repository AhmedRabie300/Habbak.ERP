using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.RemoveManualDiscount;

public sealed record RemoveManualDiscountCommand(long CheckId) : IRequest;

public sealed class RemoveManualDiscountCommandHandler(IApplicationDbContext db) : IRequestHandler<RemoveManualDiscountCommand>
{
    public async Task Handle(RemoveManualDiscountCommand request, CancellationToken cancellationToken)
    {
        var check = await db.Checks.FirstOrDefaultAsync(c => c.Id == request.CheckId, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.CheckId);

        check.ManualDiscountType = null;
        check.ManualDiscountValue = null;
        check.ManualDiscountReason = null;

        await db.SaveChangesAsync(cancellationToken);
    }
}
