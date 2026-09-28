using Habbak.ERP.API.Auth;
using Habbak.ERP.API.Contracts;
using Habbak.ERP.Application.Sales.SalesInvoices.Commands.CancelSalesInvoice;
using Habbak.ERP.Application.Sales.SalesInvoices.Commands.CreateSalesInvoice;
using Habbak.ERP.Application.Sales.SalesInvoices.Commands.PostSalesInvoice;
using Habbak.ERP.Application.Sales.SalesInvoices.Commands.RejectSalesInvoice;
using Habbak.ERP.Application.Sales.SalesInvoices.Commands.UpdateSalesInvoice;
using Habbak.ERP.Application.Sales.SalesInvoices.Dtos;
using Habbak.ERP.Application.Sales.SalesInvoices.Queries.GetPostedSalesInvoicesList;
using Habbak.ERP.Application.Sales.SalesInvoices.Queries.GetSalesInvoiceById;
using Habbak.ERP.Application.Sales.SalesInvoices.Queries.GetSalesInvoicesList;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Sales;

/// <summary>/sales/invoices — screen #7 (04-Module-Sales.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("SALES_INVOICES")]
[MaskFields("SalesInvoice")]
[MaskFields("Customer")]
[Route("api/v1/sales/invoices")]
public class SalesInvoicesController(ISender mediator) : ControllerBase
{
    public sealed record CreateSalesInvoiceRequest(
        long? BranchId, long CustomerId, DateOnly InvoiceDate, long? SourceOrderId, SalesInvoicePaymentType PaymentType,
        bool CreditLimitOverrideApproved, decimal TaxAmount, decimal? DiscountAmount, IReadOnlyList<SalesInvoiceLineInput> Lines);

    public sealed record UpdateSalesInvoiceRequest(
        string RowVersion, long? BranchId, long CustomerId, DateOnly InvoiceDate, SalesInvoicePaymentType PaymentType,
        bool CreditLimitOverrideApproved, decimal TaxAmount, decimal? DiscountAmount, IReadOnlyList<SalesInvoiceLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetSalesInvoicesListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    /// <summary>Feeds screen #8's "convert to delivery order" picker (InvoiceWithIssue cycle).</summary>
    [HttpGet("posted")]
    public async Task<IActionResult> GetPosted(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPostedSalesInvoicesListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetSalesInvoiceByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesInvoiceRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateSalesInvoiceCommand
        {
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            InvoiceDate = request.InvoiceDate,
            SourceOrderId = request.SourceOrderId,
            PaymentType = request.PaymentType,
            CreditLimitOverrideApproved = request.CreditLimitOverrideApproved,
            TaxAmount = request.TaxAmount,
            DiscountAmount = request.DiscountAmount,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSalesInvoiceRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSalesInvoiceCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            InvoiceDate = request.InvoiceDate,
            PaymentType = request.PaymentType,
            CreditLimitOverrideApproved = request.CreditLimitOverrideApproved,
            TaxAmount = request.TaxAmount,
            DiscountAmount = request.DiscountAmount,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [ScreenButton("SALES_INVOICES", "Post")]
    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new PostSalesInvoiceCommand(id, IdempotencyHeader.Read(this)), cancellationToken);
        return NoContent();
    }

    [ScreenButton("SALES_INVOICES", "Reject")]
    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectSalesInvoiceCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("SALES_INVOICES", "Cancel")]
    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelSalesInvoiceCommand(id, IdempotencyHeader.Read(this)), cancellationToken);
        return NoContent();
    }
}
