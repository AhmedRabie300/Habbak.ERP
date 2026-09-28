using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Common.Navigation.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Common;

/// <summary>/navigation (00-System-Wide-Corrections-01.md, section 3).</summary>
[ApiController]
[Authorize]
[AnySignedInUser]
[Route("api/v1/navigation")]
public class NavigationController(ISender mediator) : ControllerBase
{
    [HttpGet("menu")]
    public async Task<IActionResult> Menu([FromQuery] string lang = "ar", CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetMenuTreeQuery(lang), cancellationToken));
}
