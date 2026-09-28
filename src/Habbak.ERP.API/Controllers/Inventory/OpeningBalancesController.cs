using Habbak.ERP.API.Auth;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/opening-balances — screen #7 (02-Module-Inventory-Manufacturing.md, section
/// 5). Redesigned per My Remarks/Remarks2.md (bugs 1.2/2.5/2.6, feature 3.7) into a proper
/// Master/Detail document — same WarehouseDocument/WarehouseDocumentLine pair every other
/// warehouse-document screen already uses, DocumentType fixed to OpeningBalance (destination-only,
/// exactly like Stock In).</summary>
[Screen("INVENTORY_OPENING_BALANCES")]
[Route("api/v1/inventory/opening-balances")]
public class OpeningBalancesController(ISender mediator) : WarehouseDocumentsControllerBase(mediator)
{
    protected override WarehouseDocumentType FixedDocumentType => WarehouseDocumentType.OpeningBalance;
}
