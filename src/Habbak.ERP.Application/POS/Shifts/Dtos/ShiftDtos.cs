namespace Habbak.ERP.Application.POS.Shifts.Dtos;

public sealed class ShiftDenominationCountDto
{
    public required string CountType { get; init; }
    public required decimal DenominationValue { get; init; }
    public required int Count { get; init; }
}

public sealed class ShiftListItemDto
{
    public required long Id { get; init; }
    public required long POSTerminalId { get; init; }
    public required string POSTerminalNameAr { get; init; }
    public required string POSTerminalNameEn { get; init; }
    public required long CashierUserId { get; init; }
    public required string Status { get; init; }
    public required DateTime OpenedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public required decimal OpeningCashAmount { get; init; }
    public decimal? ActualClosingCashAmount { get; init; }
    public decimal? DifferenceAmount { get; init; }
}

public sealed class ShiftDetailDto
{
    public required long Id { get; init; }
    public required string RowVersion { get; init; }
    public required long POSTerminalId { get; init; }
    public required string POSTerminalNameAr { get; init; }
    public required string POSTerminalNameEn { get; init; }
    public required long CashierUserId { get; init; }
    public required string Status { get; init; }
    public required DateTime OpenedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public required decimal OpeningCashAmount { get; init; }
    public decimal? ExpectedClosingCashAmount { get; init; }
    public decimal? ActualClosingCashAmount { get; init; }
    public decimal? DifferenceAmount { get; init; }
    public long? ClosedByUserId { get; init; }
    public required IReadOnlyList<ShiftDenominationCountDto> DenominationCounts { get; init; }
}
