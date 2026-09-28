using Habbak.ERP.API.Auth;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

[Screen("INVENTORY_PRODUCTION_ISSUES")]
[Route("api/v1/inventory/production-issues")]
public class ProductionIssuesController(ISender mediator) : ProductionDocumentsControllerBase(mediator)
{
    protected override WarehouseDocumentType FixedDocumentType => WarehouseDocumentType.ProductionIssue;
}
