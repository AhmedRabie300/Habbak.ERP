namespace Habbak.ERP.Application.Accounting.Vouchers.Dtos;

public sealed class VoucherListItemDto
{
    public required long Id { get; init; }
    public required string VoucherType { get; init; }
    public required string VoucherNumber { get; init; }
    public required DateOnly VoucherDate { get; init; }
    public required long TreasuryAccountId { get; init; }
    public string? Description { get; init; }
    public required string CounterpartyType { get; init; }
    public required decimal Amount { get; init; }
    public required string Status { get; init; }
}

public sealed class VoucherDetailDto
{
    public required long Id { get; init; }
    public Guid? PublicId { get; init; }
    public long? BranchId { get; init; }
    public required string VoucherType { get; init; }
    public required string VoucherNumber { get; init; }
    public required DateOnly VoucherDate { get; init; }
    public required long TreasuryAccountId { get; init; }
    public string? Description { get; init; }
    public required string CounterpartyType { get; init; }
    public long? CounterpartyId { get; init; }
    public long? DirectAccountId { get; init; }
    public required decimal Amount { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal ExchangeRate { get; init; }
    public required decimal BaseCurrencyAmount { get; init; }
    public long? RelatedInvoiceId { get; init; }
    public required string Status { get; init; }
    public long? JournalEntryId { get; init; }

    /// <summary>Base64-encoded RowVersion (00-Frontend-Specs.md, section 7).</summary>
    public required string RowVersion { get; init; }
}
