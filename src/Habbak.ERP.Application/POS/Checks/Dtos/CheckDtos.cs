namespace Habbak.ERP.Application.POS.Checks.Dtos;

public sealed class CheckLineDto
{
    public required long Id { get; init; }
    public required int LineNumber { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal DiscountAmount { get; init; }
    public required decimal LineTotal { get; init; }
    public required bool IsPriceManuallyOverridden { get; init; }
    public string? Note { get; init; }
    public DateTime? SentToKitchenAt { get; init; }
}

public sealed class CheckListItemDto
{
    public required long Id { get; init; }
    public required string CheckCode { get; init; }
    public long? TableId { get; init; }
    public string? TableCode { get; init; }
    public required string OrderType { get; init; }
    public required string Status { get; init; }
    public required int LineCount { get; init; }
    public required decimal Total { get; init; }
    public required DateTime OpenedAtUtc { get; init; }
}

public sealed class CheckDetailDto
{
    public required long Id { get; init; }
    public required string RowVersion { get; init; }
    public required long POSTerminalId { get; init; }
    public required long ShiftId { get; init; }
    public required string CheckCode { get; init; }
    public long? TableId { get; init; }
    public string? TableCode { get; init; }
    public required string OrderType { get; init; }
    public required string Status { get; init; }
    public long? CustomerId { get; init; }
    public string? CustomerNameAr { get; init; }
    public decimal? CustomerLoyaltyPointsBalance { get; init; }
    public long? MergedIntoCheckId { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal DiscountTotal { get; init; }
    public string? ManualDiscountType { get; init; }
    public decimal? ManualDiscountValue { get; init; }
    public string? ManualDiscountReason { get; init; }
    public required decimal ManualDiscountAmount { get; init; }
    public decimal? LoyaltyPointsToRedeem { get; init; }
    public required decimal LoyaltyDiscountAmount { get; init; }

    /// <summary>Net of the lines after every discount — before service charge and VAT.</summary>
    public required decimal Total { get; init; }

    public required decimal ServiceChargeAmount { get; init; }
    public required decimal TaxAmount { get; init; }

    /// <summary>
    /// What the cashier actually collects: <see cref="Total"/> plus service charge and VAT.
    /// Computed by the same calculator the payment command charges with, so a client that pays this
    /// figure cannot be rejected for mismatching it. Excludes any tip, which is entered at payment.
    /// </summary>
    public required decimal PayableTotal { get; init; }

    public required IReadOnlyList<CheckLineDto> Lines { get; init; }
}
