using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Approvals.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Approvals;

/// <summary>
/// Docs/Modules/00-Project-Overview.md §12.2 — the unified Screen registry. Read-only: rows come
/// from ScreenSeedData, not a CRUD screen (same precedent as CodingRules' own ScreenCodeCatalog).
/// LookupReads — any signed-in user may read it, same as Countries/Cities/Banks: it is reference
/// data other screens (the workflow-assignment editor) pick from, not gated content itself.
/// </summary>
[ApiController]
[Authorize]
[Screen("SETTINGS_APPROVAL_WORKFLOWS", LookupReads = true)]
[Route("api/v1/screens")]
public class ScreensController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetScreensListQuery(), cancellationToken));
}
