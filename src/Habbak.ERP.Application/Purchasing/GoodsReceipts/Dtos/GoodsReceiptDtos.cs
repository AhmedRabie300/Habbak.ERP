namespace Habbak.ERP.Application.Purchasing.GoodsReceipts.Dtos;

public sealed class GoodsReceiptListItemDto
{
    public required long Id { get; init; }
    public required string ReceiptNumber { get; init; }
    public required DateOnly ReceiptDate { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseCode { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required int LineCount { get; init; }
    public required string Status { get; init; }
}

public sealed class GoodsReceiptDetailDto
{
    public required long Id { get; init; }
    public required string ReceiptNumber { get; init; }
    public required DateOnly ReceiptDate { get; init; }
    public long? BranchId { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseCode { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    /// <summary>Null on a receipt raised straight off an invoice (the "invoice only" cycle).</summary>
    public long? PurchaseOrderId { get; init; }
    public string? PurchaseOrderNumber { get; init; }

    /// <summary>Null on a receipt raised from a purchase order.</summary>
    public long? PurchaseInvoiceId { get; init; }
    public string? PurchaseInvoiceNumber { get; init; }
    public required string Status { get; init; }
    public string? Notes { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<GoodsReceiptLineDto> Lines { get; init; }
}

public sealed class GoodsReceiptLineDto
{
    public long? Id { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal AcceptedQuantity { get; init; }
    public required decimal RejectedQuantity { get; init; }
    public string? RejectedReason { get; init; }
    public long? RejectedWarehouseId { get; init; }
    public required decimal UnitCost { get; init; }
    public required long UnitId { get; init; }
    public required string UnitCode { get; init; }

    /// <summary>Base units in one UnitId, and the line's quantity and price in base units.</summary>
    public required decimal UnitFactor { get; init; }
    public required decimal BaseQuantity { get; init; }
    public required decimal BaseUnitCost { get; init; }
    public decimal? ExpectedQuantity { get; init; }
    public decimal? VarianceQuantity { get; init; }
    public string? VarianceReason { get; init; }
    public string? BatchNumber { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public required string QualityCheckStatus { get; init; }
    public string? QualityCheckNotes { get; init; }
}

public sealed record GoodsReceiptLineInput(
    long ItemId, decimal Quantity, decimal AcceptedQuantity, decimal RejectedQuantity, string? RejectedReason,
    long? RejectedWarehouseId, decimal UnitCost, long? UnitId, string? VarianceReason, string? BatchNumber,
    DateOnly? ExpiryDate, string QualityCheckStatus, string? QualityCheckNotes);

/// <summary>Feeds screen #6's create form — orders that can still receive goods against them.</summary>
public sealed record PostableOrderLineDto(long ItemId, string ItemCode, string ItemNameAr, decimal RemainingQuantity, long UnitId, string UnitCode);

public sealed record PostablePurchaseOrderDto(
    long Id, string OrderNumber, long SupplierId, string SupplierCode, IReadOnlyList<PostableOrderLineDto> Lines);
