using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Settings.Commands.UpdateProductionSalesModeSetting;

/// <summary>Screen #21 — only Mode is editable; ScopeType/ScopeId are fixed after creation since
/// changing scope is really a different setting (delete and re-add instead).</summary>
public sealed record UpdateProductionSalesModeSettingCommand(long Id, ProductionSalesMode Mode) : IRequest;

public sealed class UpdateProductionSalesModeSettingCommandHandler(IApplicationDbContext db)
    : IRequestHandler<UpdateProductionSalesModeSettingCommand>
{
    public async Task Handle(UpdateProductionSalesModeSettingCommand request, CancellationToken cancellationToken)
    {
        var setting = await db.ProductionSalesModeSettings.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductionSalesModeSetting), request.Id);

        setting.Mode = request.Mode;

        await db.SaveChangesAsync(cancellationToken);
    }
}
