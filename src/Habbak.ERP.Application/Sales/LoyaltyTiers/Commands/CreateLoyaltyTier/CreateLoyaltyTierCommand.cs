using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.LoyaltyTiers.Commands.CreateLoyaltyTier;

public sealed record CreateLoyaltyTierCommand : IRequest<long>
{
    public string? Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public int DisplayOrder { get; init; }
    public decimal MinPointsThreshold { get; init; }
    public decimal EarnRateMultiplier { get; init; } = 1.0m;
}

public sealed class CreateLoyaltyTierCommandValidator : AbstractValidator<CreateLoyaltyTierCommand>
{
    public CreateLoyaltyTierCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MinPointsThreshold).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EarnRateMultiplier).GreaterThan(0);
    }
}

public sealed class CreateLoyaltyTierCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateLoyaltyTierCommand, long>
{
    public async Task<long> Handle(CreateLoyaltyTierCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("SALES_LOYALTY_TIERS", request.Code, cancellationToken);

        var codeExists = await db.LoyaltyTiers
            .AnyAsync(t => t.CompanyId == currentCompanyContext.CompanyId && t.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("SALES-LOYALTY-TIER-CODE-EXISTS", "توجد شريحة ولاء أخرى بنفس الكود بالفعل.");
        }

        var tier = new LoyaltyTier
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            DisplayOrder = request.DisplayOrder,
            MinPointsThreshold = request.MinPointsThreshold,
            EarnRateMultiplier = request.EarnRateMultiplier,
            IsActive = true
        };

        db.LoyaltyTiers.Add(tier);
        await db.SaveChangesAsync(cancellationToken);

        return tier.Id;
    }
}
