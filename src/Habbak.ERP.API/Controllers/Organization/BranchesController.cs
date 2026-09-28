using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Organization.Branches.Commands.CreateBranch;
using Habbak.ERP.Application.Organization.Branches.Commands.DeleteBranch;
using Habbak.ERP.Application.Organization.Branches.Commands.UpdateBranch;
using Habbak.ERP.Application.Organization.Branches.Queries.GetBranchesList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Organization;

/// <summary>
/// /organization/branches — company-wide master data. A Branch-linked cost center type's values
/// mirror this screen's own active records (see DimensionsController).
/// </summary>
[ApiController]
[Authorize]
[Screen("ORG_BRANCHES", LookupReads = true)]
[Route("api/v1/organization/branches")]
public class BranchesController(ISender mediator) : ControllerBase
{
    public sealed record CreateBranchRequest(string? Code, string NameAr, string NameEn);
    public sealed record UpdateBranchRequest(string NameAr, string NameEn, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetBranchesListQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateBranchCommand(request.Code, request.NameAr, request.NameEn), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateBranchRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateBranchCommand(id, request.NameAr, request.NameEn, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteBranchCommand(id), cancellationToken);
        return NoContent();
    }
}
