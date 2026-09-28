namespace Habbak.ERP.Application.Sales.SalesInvoices.Dtos;

public sealed record SalesInvoiceLineInput(long ItemId, decimal Quantity, decimal UnitPrice, decimal? DiscountAmount);

public sealed class SalesInvoiceLineDto
{
    public long? Id { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public decimal? DiscountAmount { get; init; }
    public required decimal LineTotal { get; init; }
}

public sealed class SalesInvoiceListItemDto
{
    public required long Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required string CustomerNameAr { get; init; }
    public required string PaymentType { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal AmountPaid { get; init; }
    public required string Status { get; init; }
}

public sealed class SalesInvoiceDetailDto
{
    public required long Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required string CustomerNameAr { get; init; }
    public long? SourceOrderId { get; init; }
    public string? SourceOrderNumber { get; init; }
    public required string PaymentType { get; init; }
    public required bool CreditLimitOverrideApproved { get; init; }
    public required decimal Subtotal { get; init; }
    public decimal? DiscountAmount { get; init; }
    public required decimal TaxAmount { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal AmountPaid { get; init; }
    public required string Status { get; init; }

    /// <summary>The entry the invoice posted, and the one that reversed it if it was cancelled.</summary>
    public long? JournalEntryId { get; init; }
    public string? JournalEntryNumber { get; init; }
    public long? ReversalJournalEntryId { get; init; }
    public string? ReversalJournalEntryNumber { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<SalesInvoiceLineDto> Lines { get; init; }
}
