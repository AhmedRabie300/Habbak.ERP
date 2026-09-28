using Habbak.ERP.API.Auth;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

[Screen("INVENTORY_STOCK_IN")]
[Route("api/v1/inventory/stock-in")]
public class StockInController(ISender mediator) : WarehouseDocumentsControllerBase(mediator)
{
    protected override WarehouseDocumentType FixedDocumentType => WarehouseDocumentType.StockIn;
}
