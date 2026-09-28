namespace Habbak.ERP.Application.Sales.Settings.Dtos;

public sealed class LoyaltyProgramSettingsDto
{
    public required decimal PointsEarnRate { get; init; }
    public required decimal PointsRedemptionValue { get; init; }
}
