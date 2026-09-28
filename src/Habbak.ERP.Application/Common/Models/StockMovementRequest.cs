using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Application.Common.Models;

/// <summary>
/// Everything a calling module needs to provide to have IStockMovementService post one inbound or
/// outbound stock movement (02-Module-Inventory-Manufacturing.md, section 2.2).
/// </summary>
public sealed class StockMovementRequest
{
    public required long CompanyId { get; init; }
    public required long WarehouseId { get; init; }
    public required long ItemId { get; init; }
    public required TransactionType TransactionType { get; init; }

    /// <summary>Always positive — direction is derived from TransactionType.</summary>
    public required decimal Quantity { get; init; }

    public required decimal UnitCost { get; init; }
    public required DateOnly TransactionDate { get; init; }

    /// <summary>Left null to let the service default it from TransactionDate + Item.ShelfLifeDays
    /// for a tracked item's inbound movement (rule 32) — pass an explicit value when the actual
    /// date printed on the goods differs.</summary>
    public DateOnly? ExpiryDate { get; init; }

    /// <summary>Required when Item.IsTracked = true (rule 8).</summary>
    public string? BatchNumber { get; init; }

    /// <summary>Polymorphic reference — fixed values: WarehouseDocument, InventoryCount,
    /// ProductionOrder, POSSale.</summary>
    public string? SourceDocumentType { get; init; }
    public long? SourceDocumentId { get; init; }
}
