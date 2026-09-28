using Habbak.ERP.API.Auth;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/inventory-adjustments — screen #15 (02-Module-Inventory-Manufacturing.md,
/// section 5): a standalone manual InventoryAdjustment, outside the full count cycle (screen #14's
/// CloseInventoryCountCommand posts its own InventoryAdjustment documents directly, bypassing this
/// Draft→Posted screen entirely, same automatic-document rule as ProductionIssue/Receipt).</summary>
[Screen("INVENTORY_ADJUSTMENTS")]
[Route("api/v1/inventory/inventory-adjustments")]
public class InventoryAdjustmentsController(ISender mediator) : WarehouseDocumentsControllerBase(mediator)
{
    protected override WarehouseDocumentType FixedDocumentType => WarehouseDocumentType.InventoryAdjustment;
}
