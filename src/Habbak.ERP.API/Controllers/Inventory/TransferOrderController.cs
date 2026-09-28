using Habbak.ERP.API.Auth;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

[Screen("INVENTORY_TRANSFER_ORDER", "INVENTORY_TRANSFER_RECEIPT")]
[Route("api/v1/inventory/transfer-order")]
public class TransferOrderController(ISender mediator) : WarehouseDocumentsControllerBase(mediator)
{
    protected override WarehouseDocumentType FixedDocumentType => WarehouseDocumentType.TransferOrder;
}
