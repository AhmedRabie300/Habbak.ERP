using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Accounting.Dimensions.Commands.CreateAccountDimensionLink;
using Habbak.ERP.Application.Accounting.Dimensions.Commands.CreateDimension;
using Habbak.ERP.Application.Accounting.Dimensions.Commands.CreateDimensionValue;
using Habbak.ERP.Application.Accounting.Dimensions.Commands.DeleteAccountDimensionLink;
using Habbak.ERP.Application.Accounting.Dimensions.Commands.DeleteDimension;
using Habbak.ERP.Application.Accounting.Dimensions.Commands.DeleteDimensionValue;
using Habbak.ERP.Application.Accounting.Dimensions.Commands.UpdateDimension;
using Habbak.ERP.Application.Accounting.Dimensions.Queries.GetAccountDimensionLinks;
using Habbak.ERP.Application.Accounting.Dimensions.Queries.GetDimensionById;
using Habbak.ERP.Application.Accounting.Dimensions.Queries.GetDimensionsList;
using Habbak.ERP.Application.Accounting.Dimensions.Queries.GetDimensionValuesList;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>
/// /accounting/dimensions — "الأبعاد التحليلية وقيمها" (01-Module-Accounting.md, section 5,
/// screen 2). Not a paginated GetList screen: a company's dimension tree is always small.
/// </summary>
[ApiController]
[Authorize]
[Screen("ACCOUNTING_DIMENSIONS", LookupReads = true)]
[Route("api/v1/accounting/dimensions")]
public class DimensionsController(ISender mediator) : ControllerBase
{
    public sealed record CreateDimensionRequest(string? Code, string NameAr, string NameEn, CostCenterLinkedEntityType LinkedEntityType);
    public sealed record UpdateDimensionRequest(string NameAr, string NameEn, bool IsActive);
    public sealed record CreateDimensionValueRequest(string? Code, string NameAr, string NameEn, long? ParentId);
    public sealed record CreateLinkRequest(long AccountId, long DimensionId, int DisplayOrder, bool IsMandatory);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDimensionsListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDimensionByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDimensionRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateDimensionCommand(request.Code, request.NameAr, request.NameEn, request.LinkedEntityType), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateDimensionRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateDimensionCommand(id, request.NameAr, request.NameEn, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteDimensionCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("{dimensionId:long}/values")]
    public async Task<IActionResult> GetValues(long dimensionId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDimensionValuesListQuery(dimensionId), cancellationToken));

    [HttpPost("{dimensionId:long}/values")]
    public async Task<IActionResult> CreateValue(long dimensionId, [FromBody] CreateDimensionValueRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(
            new CreateDimensionValueCommand(dimensionId, request.Code, request.NameAr, request.NameEn, request.ParentId), cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("values/{id:long}")]
    public async Task<IActionResult> DeleteValue(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteDimensionValueCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("links")]
    public async Task<IActionResult> GetLinks([FromQuery] long accountId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAccountDimensionLinksQuery(accountId), cancellationToken));

    [HttpPost("links")]
    public async Task<IActionResult> CreateLink([FromBody] CreateLinkRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(
            new CreateAccountDimensionLinkCommand(request.AccountId, request.DimensionId, request.DisplayOrder, request.IsMandatory),
            cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("links/{id:long}")]
    public async Task<IActionResult> DeleteLink(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteAccountDimensionLinkCommand(id), cancellationToken);
        return NoContent();
    }
}
