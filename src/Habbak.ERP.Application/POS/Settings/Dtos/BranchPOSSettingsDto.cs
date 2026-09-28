namespace Habbak.ERP.Application.POS.Settings.Dtos;

public sealed class BranchPOSSettingsDto
{
    public required long BranchId { get; init; }
    public required string OperationMode { get; init; }
    public required bool TipsEnabled { get; init; }
    public required bool ServiceChargeEnabled { get; init; }
    public required decimal ServiceChargeRate { get; init; }
    public required bool VatEnabled { get; init; }
    public required decimal VatRate { get; init; }
    public required bool AllowSplitPayment { get; init; }
    public required bool LoyaltyRedemptionEnabledAtPOS { get; init; }
    public required bool ETAReceiptEnabled { get; init; }
    public required decimal CashRoundingIncrement { get; init; }
    public required decimal MaxAllowedShiftCashDifference { get; init; }
    public required string PostingMode { get; init; }
    public required decimal ShiftVarianceEmployeeLiabilityThreshold { get; init; }
}
