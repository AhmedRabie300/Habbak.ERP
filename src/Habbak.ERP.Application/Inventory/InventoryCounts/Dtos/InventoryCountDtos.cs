using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Application.Inventory.InventoryCounts.Dtos;

public sealed class InventoryCountListItemDto
{
    public required long Id { get; init; }
    public required string CountNumber { get; init; }
    public required DateOnly CountDate { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseCode { get; init; }
    public required string CountType { get; init; }
    public required string Status { get; init; }
    public required int LineCount { get; init; }
}

public sealed class InventoryCountDetailDto
{
    public required long Id { get; init; }
    public required string CountNumber { get; init; }
    public required DateOnly CountDate { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseCode { get; init; }
    public required string CountType { get; init; }
    public required string Status { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on settlement/close actions for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<InventoryCountLineDto> Lines { get; init; }
}

public sealed class InventoryCountLineDto
{
    public required long Id { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    /// <summary>In the item's base unit.</summary>
    public required decimal SystemQuantity { get; init; }

    /// <summary>In UnitId (UnitFactor base units make one of it).</summary>
    public decimal? CountedQuantity { get; init; }

    /// <summary>In the item's base unit: CountedQuantity × UnitFactor − SystemQuantity.</summary>
    public decimal? VarianceQuantity { get; init; }
    public required long UnitId { get; init; }
    public string? UnitCode { get; init; }
    public string? UnitNameAr { get; init; }
    public required decimal UnitFactor { get; init; }
    public string? BaseUnitCode { get; init; }
    public required string SettlementDecision { get; init; }
    public string? SettlementReason { get; init; }
}

/// <summary>CountedQuantity is in UnitId — the line's current unit when left out.</summary>
public sealed record CountedQuantityInput(long LineId, decimal CountedQuantity, long? UnitId = null);

public sealed record SettleInventoryCountLineInput(long LineId, SettlementDecision Decision, string? SettlementReason);

public sealed record CloseInventoryCountResult(string Status, long? InventoryAdjustmentDocumentId);
