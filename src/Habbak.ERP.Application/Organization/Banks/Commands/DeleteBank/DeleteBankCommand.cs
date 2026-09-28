using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Banks.Commands.DeleteBank;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteBankCommand(long Id) : IRequest;

public sealed class DeleteBankCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteBankCommand>
{
    public async Task Handle(DeleteBankCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Banks.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Bank), request.Id);

        db.Banks.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
