namespace Habbak.ERP.Application.Purchasing.PurchaseInvoices.Dtos;

public sealed class PurchaseInvoiceListItemDto
{
    public required long Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required DateOnly DueDate { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string CurrencyCode { get; init; }
    public required int LineCount { get; init; }
    public required string Status { get; init; }
}

public sealed class PurchaseInvoiceDetailDto
{
    public required long Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required DateOnly DueDate { get; init; }
    public long? BranchId { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public string? SupplierInvoiceNumber { get; init; }
    public long? PurchaseOrderId { get; init; }
    public string? PurchaseOrderNumber { get; init; }
    public long? GoodsReceiptId { get; init; }
    public string? GoodsReceiptNumber { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal ExchangeRate { get; init; }
    public required string PaymentTerms { get; init; }
    public required string Status { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal TaxAmount { get; init; }
    public required decimal TotalAmount { get; init; }
    public decimal? DiscountAmount { get; init; }
    public string? DiscountReason { get; init; }
    public required decimal AmountPaid { get; init; }
    public required decimal AdditionalCosts { get; init; }
    public string? AdditionalCostAllocationMethod { get; init; }
    public decimal? CommissionRate { get; init; }
    public decimal? CommissionAmount { get; init; }
    public long? CommissionAccountId { get; init; }
    public string? Notes { get; init; }

    /// <summary>The entry the invoice posted, and the one that reversed it if it was cancelled.</summary>
    public long? JournalEntryId { get; init; }
    public string? JournalEntryNumber { get; init; }
    public long? ReversalJournalEntryId { get; init; }
    public string? ReversalJournalEntryNumber { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<PurchaseInvoiceLineDto> Lines { get; init; }
}

public sealed class PurchaseInvoiceLineDto
{
    public long? Id { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal ReceivedQuantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal TotalPrice { get; init; }
    public decimal? DiscountAmount { get; init; }
    public required long UnitId { get; init; }
    public required string UnitCode { get; init; }

    /// <summary>Base units in one UnitId, and the line's quantity and price in base units.</summary>
    public required decimal UnitFactor { get; init; }
    public required decimal BaseQuantity { get; init; }
    public required decimal BaseUnitCost { get; init; }
    public required decimal AllocatedAdditionalCost { get; init; }
    public decimal? AllocationPercentage { get; init; }
    public decimal? Weight { get; init; }

    /// <summary>The order line this bills, when it came off one (Remarks6).</summary>
    public long? PurchaseOrderLineId { get; init; }
}

/// <summary>
/// PurchaseOrderLineId names the order line this bills (Remarks6) — set on every line the screen
/// loaded from the order, null on a manual line and on an invoice that has no order behind it.
/// </summary>
public sealed record PurchaseInvoiceLineInput(
    long ItemId, decimal Quantity, decimal ReceivedQuantity, decimal UnitPrice, decimal? DiscountAmount,
    long? UnitId, decimal? AllocationPercentage, decimal? Weight, long? PurchaseOrderLineId = null);
