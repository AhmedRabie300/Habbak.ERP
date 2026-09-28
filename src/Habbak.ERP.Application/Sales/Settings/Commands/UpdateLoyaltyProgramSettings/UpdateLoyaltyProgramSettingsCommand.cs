using FluentValidation;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.Settings.Commands.UpdateLoyaltyProgramSettings;

public sealed record UpdateLoyaltyProgramSettingsCommand : IRequest
{
    public required decimal PointsEarnRate { get; init; }
    public required decimal PointsRedemptionValue { get; init; }
}

public sealed class UpdateLoyaltyProgramSettingsCommandValidator : AbstractValidator<UpdateLoyaltyProgramSettingsCommand>
{
    public UpdateLoyaltyProgramSettingsCommandValidator()
    {
        RuleFor(x => x.PointsEarnRate).GreaterThan(0);
        RuleFor(x => x.PointsRedemptionValue).GreaterThan(0);
    }
}

public sealed class UpdateLoyaltyProgramSettingsCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdateLoyaltyProgramSettingsCommand>
{
    public async Task Handle(UpdateLoyaltyProgramSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await db.LoyaltyProgramSettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);

        if (settings is null)
        {
            settings = new LoyaltyProgramSettings { CompanyId = currentCompanyContext.CompanyId };
            db.LoyaltyProgramSettingsRows.Add(settings);
        }

        settings.PointsEarnRate = request.PointsEarnRate;
        settings.PointsRedemptionValue = request.PointsRedemptionValue;

        await db.SaveChangesAsync(cancellationToken);
    }
}
