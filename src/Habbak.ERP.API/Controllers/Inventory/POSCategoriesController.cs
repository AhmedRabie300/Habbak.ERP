using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Inventory.POSCategories.Commands.CreatePOSCategory;
using Habbak.ERP.Application.Inventory.POSCategories.Commands.DeletePOSCategory;
using Habbak.ERP.Application.Inventory.POSCategories.Commands.UpdatePOSCategory;
using Habbak.ERP.Application.Inventory.POSCategories.Queries.GetPOSCategoriesList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/pos-categories — dedicated reference list (02-Module-Inventory-Manufacturing.md, section 2.1).</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_POS_CATEGORIES", LookupReads = true)]
[Route("api/v1/inventory/pos-categories")]
public class POSCategoriesController(ISender mediator) : ControllerBase
{
    public sealed record CreatePOSCategoryRequest(string? Code, string NameAr, string NameEn, int DisplayOrder);
    public sealed record UpdatePOSCategoryRequest(string NameAr, string NameEn, int DisplayOrder, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPOSCategoriesListQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePOSCategoryRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePOSCategoryCommand(request.Code, request.NameAr, request.NameEn, request.DisplayOrder), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdatePOSCategoryRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePOSCategoryCommand(id, request.NameAr, request.NameEn, request.DisplayOrder, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeletePOSCategoryCommand(id), cancellationToken);
        return NoContent();
    }
}
