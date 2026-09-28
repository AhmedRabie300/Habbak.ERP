using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.Discounts.Commands.DeleteDiscount;

public sealed record DeleteDiscountCommand(long Id) : IRequest;

public sealed class DeleteDiscountCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteDiscountCommand>
{
    public async Task Handle(DeleteDiscountCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Discounts.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Discount), request.Id);

        db.Discounts.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
