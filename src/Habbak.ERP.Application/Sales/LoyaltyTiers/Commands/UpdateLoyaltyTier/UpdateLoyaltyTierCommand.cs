using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.LoyaltyTiers.Commands.UpdateLoyaltyTier;

public sealed record UpdateLoyaltyTierCommand : IRequest
{
    public required long Id { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public int DisplayOrder { get; init; }
    public decimal MinPointsThreshold { get; init; }
    public decimal EarnRateMultiplier { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class UpdateLoyaltyTierCommandValidator : AbstractValidator<UpdateLoyaltyTierCommand>
{
    public UpdateLoyaltyTierCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MinPointsThreshold).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EarnRateMultiplier).GreaterThan(0);
    }
}

public sealed class UpdateLoyaltyTierCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateLoyaltyTierCommand>
{
    public async Task Handle(UpdateLoyaltyTierCommand request, CancellationToken cancellationToken)
    {
        var tier = await db.LoyaltyTiers.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(LoyaltyTier), request.Id);

        tier.NameAr = request.NameAr;
        tier.NameEn = request.NameEn;
        tier.DisplayOrder = request.DisplayOrder;
        tier.MinPointsThreshold = request.MinPointsThreshold;
        tier.EarnRateMultiplier = request.EarnRateMultiplier;
        tier.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
