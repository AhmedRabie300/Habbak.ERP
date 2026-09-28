using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Purchasing.Reports.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>/purchasing/reports — section 11's ten reports. #6 (تحليل أسعار الشراء التاريخية) is
/// already served by /purchasing/supplier-price-history (screen #13) — not duplicated here.</summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_REPORTS")]
[Route("api/v1/purchasing/reports")]
public class PurchasingReportsController(ISender mediator) : ControllerBase
{
    [HttpGet("purchases-by-supplier")]
    public async Task<IActionResult> PurchasesBySupplier([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchasesBySupplierReportQuery(from, to), cancellationToken));

    [HttpGet("purchases-by-item")]
    public async Task<IActionResult> PurchasesByItem([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchasesByItemReportQuery(from, to), cancellationToken));

    [HttpGet("open-purchase-orders")]
    public async Task<IActionResult> OpenPurchaseOrders([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetOpenPurchaseOrdersReportQuery(from, to), cancellationToken));

    [HttpGet("unpaid-invoices")]
    public async Task<IActionResult> UnpaidInvoices([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetUnpaidPurchaseInvoicesReportQuery(from, to), cancellationToken));

    [HttpGet("purchase-returns")]
    public async Task<IActionResult> PurchaseReturns([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchaseReturnsReportQuery(from, to), cancellationToken));

    [HttpGet("supplier-evaluation-ranking")]
    public async Task<IActionResult> SupplierEvaluationRanking([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetSupplierEvaluationRankingReportQuery(from, to), cancellationToken));

    [HttpGet("purchase-expenses")]
    public async Task<IActionResult> PurchaseExpenses([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchaseExpensesReportQuery(from, to), cancellationToken));

    [HttpGet("expiring-contracts")]
    public async Task<IActionResult> ExpiringContracts([FromQuery] int withinDays, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetExpiringSupplierContractsReportQuery(withinDays <= 0 ? 30 : withinDays), cancellationToken));

    [HttpGet("additional-cost-allocation")]
    public async Task<IActionResult> AdditionalCostAllocation([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAdditionalCostAllocationReportQuery(from, to), cancellationToken));

    [HttpGet("purchase-expenses-by-type")]
    public async Task<IActionResult> PurchaseExpensesByType([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchaseExpensesByTypeReportQuery(from, to), cancellationToken));

    [HttpGet("expired-quotes")]
    public async Task<IActionResult> ExpiredQuotes([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetExpiredRFQQuotesReportQuery(from, to), cancellationToken));
}
