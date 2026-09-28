namespace Habbak.ERP.Application.Accounting.TreasuryTransfers.Dtos;

public sealed class TreasuryTransferListItemDto
{
    public required long Id { get; init; }
    public required long FromTreasuryAccountId { get; init; }
    public required long ToTreasuryAccountId { get; init; }
    public required decimal Amount { get; init; }
    public required DateOnly TransferDate { get; init; }
    public required string Status { get; init; }
}

public sealed class TreasuryTransferDetailDto
{
    public required long Id { get; init; }
    public long? BranchId { get; init; }
    public required long FromTreasuryAccountId { get; init; }
    public required long ToTreasuryAccountId { get; init; }
    public required decimal Amount { get; init; }
    public required DateOnly TransferDate { get; init; }
    public required string Status { get; init; }
    public string? Notes { get; init; }
    public long? JournalEntryId { get; init; }

    /// <summary>Base64-encoded RowVersion (00-Frontend-Specs.md, section 7).</summary>
    public required string RowVersion { get; init; }
}
