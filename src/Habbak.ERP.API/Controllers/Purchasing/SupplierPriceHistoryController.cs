using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Purchasing.SupplierPriceHistories.Queries.GetSupplierPriceHistoryReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>/purchasing/supplier-price-history — screen #13, a report (03-Module-Purchasing.md,
/// section 11.1), read-only. Rows are recorded automatically by PostPurchaseInvoiceCommand.</summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_SUPPLIER_PRICE_HISTORY")]
[Route("api/v1/purchasing/supplier-price-history")]
public class SupplierPriceHistoryController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetReport([FromQuery] long? supplierId, [FromQuery] long? itemId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetSupplierPriceHistoryReportQuery(supplierId, itemId), cancellationToken));
}
