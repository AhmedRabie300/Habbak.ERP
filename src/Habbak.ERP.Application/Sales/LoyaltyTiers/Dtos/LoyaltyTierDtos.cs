namespace Habbak.ERP.Application.Sales.LoyaltyTiers.Dtos;

public sealed class LoyaltyTierListItemDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required int DisplayOrder { get; init; }
    public required decimal MinPointsThreshold { get; init; }
    public required decimal EarnRateMultiplier { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class LoyaltyTierDetailDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required int DisplayOrder { get; init; }
    public required decimal MinPointsThreshold { get; init; }
    public required decimal EarnRateMultiplier { get; init; }
    public required bool IsActive { get; init; }
}
