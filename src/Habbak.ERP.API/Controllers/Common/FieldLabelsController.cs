using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Common.Labels.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Common;

/// <summary>/field-labels (00-System-Wide-Corrections-01.md, section 4).</summary>
[ApiController]
[Authorize]
[AnySignedInUser]
[Route("api/v1/field-labels")]
public class FieldLabelsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string screenCode, [FromQuery] string lang = "ar", CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetFieldLabelsQuery(screenCode, lang), cancellationToken));
}
