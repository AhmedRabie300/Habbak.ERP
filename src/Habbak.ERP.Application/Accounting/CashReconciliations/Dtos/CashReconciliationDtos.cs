namespace Habbak.ERP.Application.Accounting.CashReconciliations.Dtos;

public class CashReconciliationListItemDto
{
    public required long Id { get; init; }
    public required long TreasuryAccountId { get; init; }
    public required DateOnly ReconciliationDate { get; init; }
    public required decimal ExpectedBalance { get; init; }
    public required decimal ActualBalance { get; init; }
    public required decimal DifferenceAmount { get; init; }
    public required bool IsApproved { get; init; }
}

public sealed class CashReconciliationDetailDto : CashReconciliationListItemDto
{
    public string? DifferenceReason { get; init; }
    public long? ApprovedByUserId { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }
    public required IReadOnlyList<DenominationDto> Denominations { get; init; }
}

public sealed class DenominationDto
{
    public required decimal DenominationValue { get; init; }
    public required int Count { get; init; }
}
