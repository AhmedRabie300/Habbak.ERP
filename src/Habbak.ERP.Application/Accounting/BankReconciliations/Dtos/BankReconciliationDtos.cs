namespace Habbak.ERP.Application.Accounting.BankReconciliations.Dtos;

public class BankReconciliationListItemDto
{
    public required long Id { get; init; }
    public required long BankAccountId { get; init; }
    public required DateOnly PeriodFrom { get; init; }
    public required DateOnly PeriodTo { get; init; }
    public required string Status { get; init; }
}

public sealed class BankReconciliationLineDto
{
    public required long Id { get; init; }
    public string? SystemTransactionType { get; init; }
    public long? SystemTransactionId { get; init; }
    public long? BankStatementLineId { get; init; }
    public required decimal MatchedAmount { get; init; }
    public required bool IsAutoMatched { get; init; }
}

public sealed class BankReconciliationDetailDto : BankReconciliationListItemDto
{
    public long? AdjustmentJournalEntryId { get; init; }
    public required IReadOnlyList<BankReconciliationLineDto> Lines { get; init; }
}
