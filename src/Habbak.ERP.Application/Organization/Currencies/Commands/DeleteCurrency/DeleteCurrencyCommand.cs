using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Currencies.Commands.DeleteCurrency;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteCurrencyCommand(long Id) : IRequest;

public sealed class DeleteCurrencyCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteCurrencyCommand>
{
    public async Task Handle(DeleteCurrencyCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Currencies.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Currency), request.Id);

        DefaultCurrencyRules.EnsureNotRemovingDefault(entity, keepDefault: false, keepActive: true);

        db.Currencies.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
