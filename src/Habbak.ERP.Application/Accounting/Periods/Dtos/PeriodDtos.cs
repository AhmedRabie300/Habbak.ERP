namespace Habbak.ERP.Application.Accounting.Periods.Dtos;

public sealed class PeriodListItemDto
{
    public required long Id { get; init; }
    public required DateOnly PeriodStart { get; init; }
    public required DateOnly PeriodEnd { get; init; }
    public required string Status { get; init; }
}

public sealed class ChecklistItemResultDto
{
    public required string ItemKey { get; init; }
    public required string Label { get; init; }
    public required bool IsSatisfied { get; init; }
    public required string Detail { get; init; }
}

public sealed class PeriodDetailDto
{
    public required long Id { get; init; }
    public required DateOnly PeriodStart { get; init; }
    public required DateOnly PeriodEnd { get; init; }
    public required string Status { get; init; }
    public long? ClosedByUserId { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public required IReadOnlyList<ChecklistItemResultDto> Checklist { get; init; }
    public required bool CanClose { get; init; }
}
