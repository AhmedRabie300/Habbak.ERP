using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.LoyaltyTiers.Commands.DeleteLoyaltyTier;

public sealed record DeleteLoyaltyTierCommand(long Id) : IRequest;

public sealed class DeleteLoyaltyTierCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteLoyaltyTierCommand>
{
    public async Task Handle(DeleteLoyaltyTierCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.LoyaltyTiers.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(LoyaltyTier), request.Id);

        db.LoyaltyTiers.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
