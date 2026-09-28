using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Sales.LoyaltyTiers.Commands.CreateLoyaltyTier;
using Habbak.ERP.Application.Sales.LoyaltyTiers.Commands.DeleteLoyaltyTier;
using Habbak.ERP.Application.Sales.LoyaltyTiers.Commands.UpdateLoyaltyTier;
using Habbak.ERP.Application.Sales.LoyaltyTiers.Queries.GetLoyaltyTierById;
using Habbak.ERP.Application.Sales.LoyaltyTiers.Queries.GetLoyaltyTiersList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Sales;

/// <summary>/sales/loyalty-tiers — screen #4 (04-Module-Sales.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("SALES_LOYALTY_TIERS", LookupReads = true)]
[Route("api/v1/sales/loyalty-tiers")]
public class LoyaltyTiersController(ISender mediator) : ControllerBase
{
    public sealed record CreateLoyaltyTierRequest(
        string? Code, string NameAr, string NameEn, int DisplayOrder, decimal MinPointsThreshold, decimal EarnRateMultiplier);

    public sealed record UpdateLoyaltyTierRequest(
        string NameAr, string NameEn, int DisplayOrder, decimal MinPointsThreshold, decimal EarnRateMultiplier, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetLoyaltyTiersListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetLoyaltyTierByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLoyaltyTierRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateLoyaltyTierCommand
        {
            Code = request.Code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            DisplayOrder = request.DisplayOrder,
            MinPointsThreshold = request.MinPointsThreshold,
            EarnRateMultiplier = request.EarnRateMultiplier
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateLoyaltyTierRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateLoyaltyTierCommand
        {
            Id = id,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            DisplayOrder = request.DisplayOrder,
            MinPointsThreshold = request.MinPointsThreshold,
            EarnRateMultiplier = request.EarnRateMultiplier,
            IsActive = request.IsActive
        }, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteLoyaltyTierCommand(id), cancellationToken);
        return NoContent();
    }
}
