namespace Habbak.ERP.Application.Inventory.Items.Dtos;

public sealed class ItemListItemDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required string ItemType { get; init; }
    public required string BaseUnitOfMeasureCode { get; init; }
    public required long BaseUnitOfMeasureId { get; init; }

    /// <summary>The units a line of this item may be in: the base unit (factor 1) first, then its conversions.</summary>
    public required IReadOnlyList<ItemUnitOptionDto> Units { get; init; }
    public string? TaxCode { get; init; }
    public required string Status { get; init; }
    public required bool IsActive { get; init; }
    public long? POSCategoryId { get; init; }
    public string? POSCategoryNameAr { get; init; }
    public decimal? DefaultPrice { get; init; }
    public required bool IsSellable { get; init; }
    public string? Barcode { get; init; }
}

public sealed record ItemUnitOptionDto(long UnitId, string Code, string NameAr, decimal Factor);

public sealed class ItemUnitConversionDto
{
    public required long Id { get; init; }
    public required long AlternateUnitOfMeasureId { get; init; }
    public required string AlternateUnitOfMeasureCode { get; init; }
    public required decimal ConversionFactor { get; init; }
}

public sealed class ItemWarehouseSettingsDto
{
    public required long Id { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseCode { get; init; }
    public decimal? MinStockLevel { get; init; }
    public decimal? MaxStockLevel { get; init; }
    public decimal? ReorderPoint { get; init; }
}

public sealed class BranchItemLimitDto
{
    public required long Id { get; init; }
    public required long BranchId { get; init; }
    public decimal? MinRequestQuantity { get; init; }
    public required decimal MaxRequestQuantity { get; init; }
}

public sealed class ItemDetailDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public long? ItemGroupId { get; init; }
    public long? POSCategoryId { get; init; }
    public required string ItemType { get; init; }
    public string? Barcode { get; init; }

    /// <summary>The ETA code (GS1/EGS) — optional until the ETA module exists.</summary>
    public string? TaxCode { get; init; }
    public required long BaseUnitOfMeasureId { get; init; }
    public long? PurchaseUnitOfMeasureId { get; init; }
    public long? SellUnitOfMeasureId { get; init; }
    public required string SaleMethod { get; init; }
    public required string CostMethod { get; init; }
    public decimal? DefaultPrice { get; init; }
    public required bool IsStocked { get; init; }
    public required bool IsTracked { get; init; }
    public required bool TrackSerial { get; init; }
    public int? ShelfLifeDays { get; init; }
    public decimal? StandardCost { get; init; }
    public required bool IsPurchasable { get; init; }
    public required bool IsSellable { get; init; }
    public required bool IsManufacturable { get; init; }
    public required bool AllowSubstitutes { get; init; }
    public required string Status { get; init; }
    public required bool IsActive { get; init; }
    public required IReadOnlyList<ItemUnitConversionDto> UnitConversions { get; init; }
    public required IReadOnlyList<ItemWarehouseSettingsDto> WarehouseSettings { get; init; }
    public required IReadOnlyList<BranchItemLimitDto> BranchItemLimits { get; init; }
}

/// <summary>Input shape for the nested sections of the Item edit screen — Update replaces the
/// whole set for the item in one call (diff-and-replace, same spirit as AccountCostCentersEditor
/// in 01-Module-Accounting.md).</summary>
public sealed record ItemUnitConversionInput(long AlternateUnitOfMeasureId, decimal ConversionFactor);

public sealed record ItemWarehouseSettingsInput(long WarehouseId, decimal? MinStockLevel, decimal? MaxStockLevel, decimal? ReorderPoint);

public sealed record BranchItemLimitInput(long BranchId, decimal? MinRequestQuantity, decimal MaxRequestQuantity);
