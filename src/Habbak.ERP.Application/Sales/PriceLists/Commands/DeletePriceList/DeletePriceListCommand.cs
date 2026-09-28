using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.PriceLists.Commands.DeletePriceList;

public sealed record DeletePriceListCommand(long Id) : IRequest;

public sealed class DeletePriceListCommandHandler(IApplicationDbContext db) : IRequestHandler<DeletePriceListCommand>
{
    public async Task Handle(DeletePriceListCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.PriceLists.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PriceList), request.Id);

        db.PriceLists.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
