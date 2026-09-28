using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Inventory.ItemGroups.Commands.CreateItemGroup;
using Habbak.ERP.Application.Inventory.ItemGroups.Commands.DeleteItemGroup;
using Habbak.ERP.Application.Inventory.ItemGroups.Commands.UpdateItemGroup;
using Habbak.ERP.Application.Inventory.ItemGroups.Queries.GetItemGroupsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/item-groups — dedicated reference list (02-Module-Inventory-Manufacturing.md, section 2.1).</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_ITEM_GROUPS", LookupReads = true)]
[Route("api/v1/inventory/item-groups")]
public class ItemGroupsController(ISender mediator) : ControllerBase
{
    public sealed record CreateItemGroupRequest(string? Code, string NameAr, string NameEn, long? ParentId);
    public sealed record UpdateItemGroupRequest(string NameAr, string NameEn, long? ParentId, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetItemGroupsListQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateItemGroupRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateItemGroupCommand(request.Code, request.NameAr, request.NameEn, request.ParentId), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateItemGroupRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateItemGroupCommand(id, request.NameAr, request.NameEn, request.ParentId, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteItemGroupCommand(id), cancellationToken);
        return NoContent();
    }
}
