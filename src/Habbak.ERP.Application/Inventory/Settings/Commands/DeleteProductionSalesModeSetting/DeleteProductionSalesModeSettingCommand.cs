using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;

namespace Habbak.ERP.Application.Inventory.Settings.Commands.DeleteProductionSalesModeSetting;

public sealed record DeleteProductionSalesModeSettingCommand(long Id) : IRequest;

public sealed class DeleteProductionSalesModeSettingCommandHandler(IApplicationDbContext db)
    : IRequestHandler<DeleteProductionSalesModeSettingCommand>
{
    public async Task Handle(DeleteProductionSalesModeSettingCommand request, CancellationToken cancellationToken)
    {
        var setting = await db.ProductionSalesModeSettings.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(ProductionSalesModeSetting), request.Id);

        db.ProductionSalesModeSettings.Remove(setting);
        await db.SaveChangesAsync(cancellationToken);
    }
}
