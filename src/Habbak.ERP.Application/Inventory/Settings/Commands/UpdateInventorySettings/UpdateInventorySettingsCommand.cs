using FluentValidation;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using InventorySettingsEntity = Habbak.ERP.Domain.Inventory.InventorySettings;

namespace Habbak.ERP.Application.Inventory.Settings.Commands.UpdateInventorySettings;

/// <summary>Screen #23 — upserts the single InventorySettings row for the current company.</summary>
public sealed record UpdateInventorySettingsCommand(int SlowMovingThresholdDays) : IRequest;

public sealed class UpdateInventorySettingsCommandValidator : AbstractValidator<UpdateInventorySettingsCommand>
{
    public UpdateInventorySettingsCommandValidator()
    {
        RuleFor(x => x.SlowMovingThresholdDays).GreaterThan(0);
    }
}

public sealed class UpdateInventorySettingsCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdateInventorySettingsCommand>
{
    public async Task Handle(UpdateInventorySettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await db.InventorySettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);

        if (settings is null)
        {
            settings = new InventorySettingsEntity { CompanyId = currentCompanyContext.CompanyId };
            db.InventorySettingsRows.Add(settings);
        }

        settings.SlowMovingThresholdDays = request.SlowMovingThresholdDays;

        await db.SaveChangesAsync(cancellationToken);
    }
}
