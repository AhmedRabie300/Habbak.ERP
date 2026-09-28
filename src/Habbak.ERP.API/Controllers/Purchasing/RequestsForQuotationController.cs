using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.AwardRFQ;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.CancelRFQ;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.CreateRFQ;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.DeclineRFQSupplier;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.SelectRFQSupplierQuote;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.SendRFQ;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.SetRFQSupplierQuote;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.UpdateRFQ;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Dtos;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Queries.GetRFQById;
using Habbak.ERP.Application.Purchasing.RequestsForQuotation.Queries.GetRFQsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>/purchasing/rfqs — screen #3 (03-Module-Purchasing.md, section 8), "مع مقارنة عروض
/// الموردين".</summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_RFQS")]
[Route("api/v1/purchasing/rfqs")]
public class RequestsForQuotationController(ISender mediator) : ControllerBase
{
    public sealed record CreateRFQRequest(
        long? BranchId, DateOnly RFQDate, long? PurchaseRequestId, DateOnly? RequiredDate, string? Notes,
        IReadOnlyList<RFQLineInput> Lines, IReadOnlyList<long> SupplierIds);

    public sealed record UpdateRFQRequest(
        string RowVersion, long? BranchId, DateOnly RFQDate, long? PurchaseRequestId, DateOnly? RequiredDate, string? Notes,
        IReadOnlyList<RFQLineInput> Lines, IReadOnlyList<long> SupplierIds);

    public sealed record SetQuoteRequest(
        long RFQSupplierId, long RFQLineId, decimal UnitPrice, decimal? DiscountPercentage, int? DeliveryDays, DateOnly ValidUntil, string? Notes);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetRFQsListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetRFQByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRFQRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateRFQCommand
        {
            BranchId = request.BranchId,
            RFQDate = request.RFQDate,
            PurchaseRequestId = request.PurchaseRequestId,
            RequiredDate = request.RequiredDate,
            Notes = request.Notes,
            Lines = request.Lines,
            SupplierIds = request.SupplierIds
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateRFQRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateRFQCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            RFQDate = request.RFQDate,
            PurchaseRequestId = request.PurchaseRequestId,
            RequiredDate = request.RequiredDate,
            Notes = request.Notes,
            Lines = request.Lines,
            SupplierIds = request.SupplierIds
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/send")]
    public async Task<IActionResult> Send(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SendRFQCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/quotes")]
    public async Task<IActionResult> SetQuote(long id, [FromBody] SetQuoteRequest request, CancellationToken cancellationToken)
    {
        var quoteId = await mediator.Send(new SetRFQSupplierQuoteCommand
        {
            RFQId = id,
            RFQSupplierId = request.RFQSupplierId,
            RFQLineId = request.RFQLineId,
            UnitPrice = request.UnitPrice,
            DiscountPercentage = request.DiscountPercentage,
            DeliveryDays = request.DeliveryDays,
            ValidUntil = request.ValidUntil,
            Notes = request.Notes
        }, cancellationToken);

        return Ok(new { id = quoteId });
    }

    [HttpPost("{id:long}/suppliers/{rfqSupplierId:long}/decline")]
    public async Task<IActionResult> DeclineSupplier(long id, long rfqSupplierId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeclineRFQSupplierCommand(rfqSupplierId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/quotes/{quoteId:long}/select")]
    public async Task<IActionResult> SelectQuote(long id, long quoteId, CancellationToken cancellationToken)
    {
        await mediator.Send(new SelectRFQSupplierQuoteCommand(id, quoteId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/award")]
    public async Task<IActionResult> Award(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new AwardRFQCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelRFQCommand(id), cancellationToken);
        return NoContent();
    }
}
