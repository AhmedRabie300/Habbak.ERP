using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Terminals.Commands.UpdatePOSTerminal;

public sealed record UpdatePOSTerminalCommand(long Id, string NameAr, string NameEn, long BranchId, long? DefaultWarehouseId, bool IsActive) : IRequest;

public sealed class UpdatePOSTerminalCommandValidator : AbstractValidator<UpdatePOSTerminalCommand>
{
    public UpdatePOSTerminalCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BranchId).GreaterThan(0);
    }
}

public sealed class UpdatePOSTerminalCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdatePOSTerminalCommand>
{
    public async Task Handle(UpdatePOSTerminalCommand request, CancellationToken cancellationToken)
    {
        var terminal = await db.POSTerminals.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(POSTerminal), request.Id);

        if (!await db.Branches.AnyAsync(b => b.Id == request.BranchId, cancellationToken))
        {
            throw new NotFoundException("Branch", request.BranchId);
        }

        if (request.DefaultWarehouseId is { } warehouseId && !await db.Warehouses.AnyAsync(w => w.Id == warehouseId, cancellationToken))
        {
            throw new NotFoundException("Warehouse", warehouseId);
        }

        terminal.NameAr = request.NameAr;
        terminal.NameEn = request.NameEn;
        terminal.BranchId = request.BranchId;
        terminal.DefaultWarehouseId = request.DefaultWarehouseId;
        terminal.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
