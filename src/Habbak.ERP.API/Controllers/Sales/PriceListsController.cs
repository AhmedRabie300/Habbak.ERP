using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Sales.PriceLists.Commands.CreatePriceList;
using Habbak.ERP.Application.Sales.PriceLists.Commands.DeletePriceList;
using Habbak.ERP.Application.Sales.PriceLists.Commands.UpdatePriceList;
using Habbak.ERP.Application.Sales.PriceLists.Dtos;
using Habbak.ERP.Application.Sales.PriceLists.Queries.GetPriceListById;
using Habbak.ERP.Application.Sales.PriceLists.Queries.GetPriceListsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Sales;

/// <summary>/sales/price-lists — screen #2 (04-Module-Sales.md, section 5), شاشة تفاعلية
/// (Multi-select فروع + شبكة أصناف بـ3 أعمدة سعر).</summary>
[ApiController]
[Authorize]
[Screen("SALES_PRICE_LISTS", LookupReads = true)]
[Route("api/v1/sales/price-lists")]
public class PriceListsController(ISender mediator) : ControllerBase
{
    public sealed record CreatePriceListRequest(
        string? Code, string NameAr, string NameEn, DateOnly EffectiveFromDate, DateOnly? EffectiveToDate,
        IReadOnlyList<long> BranchIds, IReadOnlyList<PriceListLineInput> Lines);

    public sealed record UpdatePriceListRequest(
        string NameAr, string NameEn, DateOnly EffectiveFromDate, DateOnly? EffectiveToDate,
        IReadOnlyList<long> BranchIds, IReadOnlyList<PriceListLineInput> Lines, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPriceListsListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPriceListByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePriceListRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePriceListCommand
        {
            Code = request.Code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            EffectiveFromDate = request.EffectiveFromDate,
            EffectiveToDate = request.EffectiveToDate,
            BranchIds = request.BranchIds,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdatePriceListRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePriceListCommand
        {
            Id = id,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            EffectiveFromDate = request.EffectiveFromDate,
            EffectiveToDate = request.EffectiveToDate,
            BranchIds = request.BranchIds,
            Lines = request.Lines,
            IsActive = request.IsActive
        }, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeletePriceListCommand(id), cancellationToken);
        return NoContent();
    }
}
