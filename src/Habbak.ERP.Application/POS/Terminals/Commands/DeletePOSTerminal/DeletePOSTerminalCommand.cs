using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Terminals.Commands.DeletePOSTerminal;

public sealed record DeletePOSTerminalCommand(long Id) : IRequest;

public sealed class DeletePOSTerminalCommandHandler(IApplicationDbContext db) : IRequestHandler<DeletePOSTerminalCommand>
{
    public async Task Handle(DeletePOSTerminalCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.POSTerminals.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(POSTerminal), request.Id);

        db.POSTerminals.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
