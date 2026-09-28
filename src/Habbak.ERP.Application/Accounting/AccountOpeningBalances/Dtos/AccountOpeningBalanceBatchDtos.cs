namespace Habbak.ERP.Application.Accounting.AccountOpeningBalances.Dtos;

public sealed class AccountOpeningBalanceBatchListItemDto
{
    public required long Id { get; init; }
    public required string BatchNumber { get; init; }
    public required DateOnly TransactionDate { get; init; }
    public required int LineCount { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Status { get; init; }
}

public sealed class AccountOpeningBalanceBatchDetailDto
{
    public required long Id { get; init; }
    public required string BatchNumber { get; init; }
    public required DateOnly TransactionDate { get; init; }
    public required string Status { get; init; }
    public long? JournalEntryId { get; init; }
    public string? Notes { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<AccountOpeningBalanceLineDto> Lines { get; init; }
}

public sealed class AccountOpeningBalanceLineDto
{
    public long? Id { get; init; }
    public required long AccountId { get; init; }
    public required string AccountCode { get; init; }
    public required string AccountNameAr { get; init; }
    public required string AccountNature { get; init; }
    public required decimal Amount { get; init; }
    public string? Notes { get; init; }
}

public sealed record AccountOpeningBalanceLineInput(long AccountId, decimal Amount, string? Notes);
