namespace Habbak.ERP.Application.Purchasing.PurchaseReturns.Dtos;

public sealed class PurchaseReturnListItemDto
{
    public required long Id { get; init; }
    public required string ReturnNumber { get; init; }
    public required DateOnly ReturnDate { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required string Reason { get; init; }
    public required int LineCount { get; init; }
    public required string Status { get; init; }
}

public sealed class PurchaseReturnDetailDto
{
    public required long Id { get; init; }
    public required string ReturnNumber { get; init; }
    public required DateOnly ReturnDate { get; init; }
    public long? BranchId { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public long? PurchaseInvoiceId { get; init; }
    public string? PurchaseInvoiceNumber { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseCode { get; init; }
    public required string Reason { get; init; }
    public required string Status { get; init; }
    public string? Notes { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<PurchaseReturnLineDto> Lines { get; init; }
}

public sealed class PurchaseReturnLineDto
{
    public long? Id { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitCost { get; init; }
    public required long UnitId { get; init; }
    public required string UnitCode { get; init; }

    /// <summary>Base units in one UnitId, and the line's quantity and price in base units.</summary>
    public required decimal UnitFactor { get; init; }
    public required decimal BaseQuantity { get; init; }
    public required decimal BaseUnitCost { get; init; }
    public string? BatchNumber { get; init; }
}

/// <summary>Quantity and UnitCost are in UnitId — the item's base unit when left out.</summary>
/// <summary>
/// PurchaseInvoiceLineId is required on a return that names an invoice (Remarks4, item 8) — it is
/// what caps the returned quantity and keeps the item honest; it stays null on a return with no
/// invoice behind it.
/// </summary>
public sealed record PurchaseReturnLineInput(
    long ItemId, decimal Quantity, decimal UnitCost, long? UnitId, string? BatchNumber, long? PurchaseInvoiceLineId = null);
