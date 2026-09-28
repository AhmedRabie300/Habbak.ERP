namespace Habbak.ERP.API.Contracts.Accounting;

public sealed record CreateTreasuryTransferRequest(
    long? BranchId,
    long FromTreasuryAccountId,
    long ToTreasuryAccountId,
    decimal Amount,
    DateOnly TransferDate,
    string? Notes);

public sealed record UpdateTreasuryTransferRequest(
    string RowVersion,
    long? BranchId,
    long FromTreasuryAccountId,
    long ToTreasuryAccountId,
    decimal Amount,
    DateOnly TransferDate,
    string? Notes);
