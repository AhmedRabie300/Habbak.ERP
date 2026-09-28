namespace Habbak.ERP.Application.Inventory.ProductionOrders.Dtos;

public sealed class ProductionOrderListItemDto
{
    public required long Id { get; init; }
    public required string OrderNumber { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseCode { get; init; }
    public required string RecipeFamilyCode { get; init; }
    public required int RecipeVersionNumber { get; init; }
    public required string OutputItemCode { get; init; }
    public required string OutputItemNameAr { get; init; }
    public required decimal PlannedQuantity { get; init; }
    public decimal? ActualQuantity { get; init; }
    public required string Status { get; init; }
}

public sealed class ProductionOrderDetailDto
{
    public required long Id { get; init; }
    public required string OrderNumber { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseCode { get; init; }
    public required long RecipeId { get; init; }
    public required string RecipeFamilyCode { get; init; }
    public required int RecipeVersionNumber { get; init; }
    public required long OutputItemId { get; init; }
    public required string OutputItemCode { get; init; }
    public required string OutputItemNameAr { get; init; }
    public required decimal RecipeOutputQuantity { get; init; }
    public required decimal PlannedQuantity { get; init; }
    public decimal? ActualQuantity { get; init; }
    public required string Status { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public required long ExecutedByUserId { get; init; }
    public required decimal StandardCost { get; init; }
    public decimal? ActualCost { get; init; }
    public long? ProductionIssueDocumentId { get; init; }
    public long? ProductionReceiptDocumentId { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    /// <summary>Recipe components scaled to PlannedQuantity — read-only preview so the edit screen
    /// shows what completing the order at the planned quantity is expected to consume, before any
    /// stock movement actually happens (that only occurs at CompleteProductionOrderCommand).</summary>
    public required IReadOnlyList<ProductionOrderComponentPreviewDto> ComponentsPreview { get; init; }
}

public sealed class ProductionOrderComponentPreviewDto
{
    public required long ComponentItemId { get; init; }
    public required string ComponentItemCode { get; init; }
    public required string ComponentItemNameAr { get; init; }
    public required decimal PlannedConsumption { get; init; }
}

public sealed record CompleteProductionOrderResult(string Status, decimal ActualCost, decimal CostVariance, long ProductionIssueDocumentId, long ProductionReceiptDocumentId);
