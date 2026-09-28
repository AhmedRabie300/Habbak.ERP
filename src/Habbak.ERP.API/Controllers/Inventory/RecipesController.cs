using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Inventory.Recipes.Commands.ApproveRecipe;
using Habbak.ERP.Application.Inventory.Recipes.Commands.CreateNewRecipeVersion;
using Habbak.ERP.Application.Inventory.Recipes.Commands.CreateRecipe;
using Habbak.ERP.Application.Inventory.Recipes.Commands.RejectRecipe;
using Habbak.ERP.Application.Inventory.Recipes.Commands.SubmitRecipe;
using Habbak.ERP.Application.Inventory.Recipes.Commands.UpdateRecipe;
using Habbak.ERP.Application.Inventory.Recipes.Dtos;
using Habbak.ERP.Application.Inventory.Recipes.Queries.GetRecipeById;
using Habbak.ERP.Application.Inventory.Recipes.Queries.GetRecipesList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/recipes — screens #16-17 (02-Module-Inventory-Manufacturing.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_RECIPES", LookupReads = true)]
[Route("api/v1/inventory/recipes")]
public class RecipesController(ISender mediator) : ControllerBase
{
    public sealed record CreateRecipeRequest(
        long OutputItemId, decimal OutputQuantity, decimal WastePercentage, DateOnly EffectiveFromDate, IReadOnlyList<RecipeLineInput> Lines);

    public sealed record UpdateRecipeRequest(
        string RowVersion, decimal OutputQuantity, decimal WastePercentage, DateOnly EffectiveFromDate, IReadOnlyList<RecipeLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetRecipesListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetRecipeByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRecipeRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateRecipeCommand
        {
            OutputItemId = request.OutputItemId,
            OutputQuantity = request.OutputQuantity,
            WastePercentage = request.WastePercentage,
            EffectiveFromDate = request.EffectiveFromDate,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateRecipeRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateRecipeCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            OutputQuantity = request.OutputQuantity,
            WastePercentage = request.WastePercentage,
            EffectiveFromDate = request.EffectiveFromDate,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SubmitRecipeCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ApproveRecipeCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectRecipeCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/new-version")]
    public async Task<IActionResult> CreateNewVersion(long id, CancellationToken cancellationToken)
    {
        var newId = await mediator.Send(new CreateNewRecipeVersionCommand(id), cancellationToken);
        return Ok(new { id = newId });
    }
}
