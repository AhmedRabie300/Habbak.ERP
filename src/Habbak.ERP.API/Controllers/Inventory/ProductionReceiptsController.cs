using Habbak.ERP.API.Auth;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

[Screen("INVENTORY_PRODUCTION_RECEIPTS")]
[Route("api/v1/inventory/production-receipts")]
public class ProductionReceiptsController(ISender mediator) : ProductionDocumentsControllerBase(mediator)
{
    protected override WarehouseDocumentType FixedDocumentType => WarehouseDocumentType.ProductionReceipt;
}
