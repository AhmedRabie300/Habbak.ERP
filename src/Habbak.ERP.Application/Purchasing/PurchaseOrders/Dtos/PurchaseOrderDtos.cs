namespace Habbak.ERP.Application.Purchasing.PurchaseOrders.Dtos;

public sealed class PurchaseOrderListItemDto
{
    public required long Id { get; init; }
    public required string OrderNumber { get; init; }
    public required DateOnly OrderDate { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string CurrencyCode { get; init; }
    public required int LineCount { get; init; }
    public required string Status { get; init; }
}

public sealed class PurchaseOrderDetailDto
{
    public required long Id { get; init; }
    public required string OrderNumber { get; init; }
    public required DateOnly OrderDate { get; init; }
    public long? BranchId { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public long? PurchaseRequestId { get; init; }
    public string? PurchaseRequestNumber { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal ExchangeRate { get; init; }
    public required string PaymentTerms { get; init; }
    public string? DeliveryTerms { get; init; }
    public DateOnly? ExpectedDeliveryDate { get; init; }
    public string? DeliveryAddress { get; init; }
    public required string Status { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal TaxAmount { get; init; }
    public required decimal TotalAmount { get; init; }
    public decimal? DiscountAmount { get; init; }
    public string? DiscountReason { get; init; }
    public string? Notes { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<PurchaseOrderLineDto> Lines { get; init; }
}

public sealed class PurchaseOrderLineDto
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
    public DateOnly? ExpectedDeliveryDate { get; init; }
    public decimal? Weight { get; init; }

    /// <summary>The request line this line came from, if any (Remarks7).</summary>
    public long? PurchaseRequestLineId { get; init; }
}

public sealed record PurchaseOrderLineInput(
    long ItemId, decimal Quantity, decimal UnitPrice, decimal? DiscountAmount, long? UnitId, DateOnly? ExpectedDeliveryDate, decimal? Weight,
    long? PurchaseRequestLineId = null);
