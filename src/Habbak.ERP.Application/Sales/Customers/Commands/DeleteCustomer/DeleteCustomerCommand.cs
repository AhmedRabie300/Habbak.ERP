using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.Customers.Commands.DeleteCustomer;

public sealed record DeleteCustomerCommand(long Id) : IRequest;

public sealed class DeleteCustomerCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteCustomerCommand>
{
    public async Task Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Customers.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        db.Customers.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
