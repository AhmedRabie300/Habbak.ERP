using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Sales.SalesQuotes.Commands.AcceptSalesQuote;
using Habbak.ERP.Application.Sales.SalesQuotes.Commands.CreateSalesQuote;
using Habbak.ERP.Application.Sales.SalesQuotes.Commands.RejectSalesQuote;
using Habbak.ERP.Application.Sales.SalesQuotes.Commands.SubmitSalesQuote;
using Habbak.ERP.Application.Sales.SalesQuotes.Commands.UpdateSalesQuote;
using Habbak.ERP.Application.Sales.SalesQuotes.Dtos;
using Habbak.ERP.Application.Sales.SalesQuotes.Queries.GetAcceptedSalesQuotesList;
using Habbak.ERP.Application.Sales.SalesQuotes.Queries.GetSalesQuoteById;
using Habbak.ERP.Application.Sales.SalesQuotes.Queries.GetSalesQuotesList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Sales;

/// <summary>/sales/quotes — screen #5 (04-Module-Sales.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("SALES_QUOTES")]
[Route("api/v1/sales/quotes")]
public class SalesQuotesController(ISender mediator) : ControllerBase
{
    public sealed record CreateSalesQuoteRequest(
        long? BranchId, long CustomerId, DateOnly QuoteDate, DateOnly ValidUntil, IReadOnlyList<SalesQuoteLineInput> Lines);

    public sealed record UpdateSalesQuoteRequest(
        string RowVersion, long? BranchId, long CustomerId, DateOnly QuoteDate, DateOnly ValidUntil,
        IReadOnlyList<SalesQuoteLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetSalesQuotesListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    /// <summary>Feeds screen #6's "convert to sales order" picker.</summary>
    [HttpGet("accepted")]
    public async Task<IActionResult> GetAccepted(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAcceptedSalesQuotesListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetSalesQuoteByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesQuoteRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateSalesQuoteCommand
        {
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            QuoteDate = request.QuoteDate,
            ValidUntil = request.ValidUntil,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSalesQuoteRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSalesQuoteCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            QuoteDate = request.QuoteDate,
            ValidUntil = request.ValidUntil,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SubmitSalesQuoteCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/accept")]
    public async Task<IActionResult> Accept(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new AcceptSalesQuoteCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectSalesQuoteCommand(id), cancellationToken);
        return NoContent();
    }
}
