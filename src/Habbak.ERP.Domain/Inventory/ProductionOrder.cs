using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>
/// أمر إنتاج (02-Module-Inventory-Manufacturing.md, section 2.6). Completing the order (rule 15)
/// scales every RecipeLine.Quantity by ActualQuantity ÷ Recipe.OutputQuantity and posts the result
/// as a pair of auto-generated, already-Posted WarehouseDocuments (ProductionIssue for the consumed
/// components, ProductionReceipt for the output item) — CompleteProductionOrderCommand records both
/// document ids here for screen #20's read-only trace, a field the module doc's own table doesn't
/// list but that every other auto-generated-document flow in this module needs for traceability.
/// </summary>
public class ProductionOrder : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    /// <summary>Points at a specific Recipe version's Id, never just the RecipeFamilyCode — must be
    /// Approved at creation time (doc's own field-table note) so completed orders stay tied to the
    /// exact BOM that was in force when they ran, even after later versions are approved (rule 31).</summary>
    public long RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    /// <summary>Not in the doc's own field table, but every other document-like entity in this
    /// module (BranchRequest.RequestNumber, WarehouseDocument.DocumentNumber, ...) carries one for
    /// the List screen — generated via ICodeGenerator, screen code INVENTORY_PRODUCTION_ORDER.</summary>
    public string OrderNumber { get; set; } = null!;

    public decimal PlannedQuantity { get; set; }
    public decimal? ActualQuantity { get; set; }

    public ProductionOrderStatus Status { get; set; } = ProductionOrderStatus.Pending;

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public long ExecutedByUserId { get; set; }

    /// <summary>Estimated at creation from the Recipe's component costs scaled to PlannedQuantity —
    /// never recomputed afterward, so it stays the baseline ActualCost is compared against (rule 16).</summary>
    public decimal StandardCost { get; set; }

    /// <summary>Computed only at completion (rule 35) — never during InProgress.</summary>
    public decimal? ActualCost { get; set; }

    public long? ProductionIssueDocumentId { get; set; }
    public WarehouseDocument? ProductionIssueDocument { get; set; }

    public long? ProductionReceiptDocumentId { get; set; }
    public WarehouseDocument? ProductionReceiptDocument { get; set; }
}
