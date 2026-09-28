using Habbak.ERP.API.Auth;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

[Screen("INVENTORY_STOCK_OUT")]
[Route("api/v1/inventory/stock-out")]
public class StockOutController(ISender mediator) : WarehouseDocumentsControllerBase(mediator)
{
    protected override WarehouseDocumentType FixedDocumentType => WarehouseDocumentType.StockOut;
}
