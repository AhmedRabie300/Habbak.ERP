using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Cities.Commands.DeleteCity;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteCityCommand(long Id) : IRequest;

public sealed class DeleteCityCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteCityCommand>
{
    public async Task Handle(DeleteCityCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Cities.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(City), request.Id);

        db.Cities.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
