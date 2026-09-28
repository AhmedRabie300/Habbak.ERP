using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Common.Coding.Commands;
using Habbak.ERP.Application.Common.Coding.Queries;
using Habbak.ERP.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Common;

/// <summary>/settings/coding-rules — per-screen manual/automatic code numbering, company-wide.</summary>
[ApiController]
[Authorize]
[Screen("SETTINGS_CODING_RULES", LookupReads = true)]
[Route("api/v1/settings/coding-rules")]
public class CodingRulesController(ISender mediator) : ControllerBase
{
    public sealed record UpsertCodingRuleRequest(
        bool IsAutomatic, CodeFormat Format, string? Prefix, int SequenceLength,
        bool IsAttachmentMandatory, bool IsDescriptionMandatory);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string lang = "ar", CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetCodingRulesQuery(lang), cancellationToken));

    [HttpGet("{screenCode}")]
    public async Task<IActionResult> GetOne(string screenCode, [FromQuery] string lang = "ar", CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetCodingRuleQuery(screenCode, lang), cancellationToken));

    [HttpPut("{screenCode}")]
    public async Task<IActionResult> Upsert(string screenCode, [FromBody] UpsertCodingRuleRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(
            new UpsertCodingRuleCommand(
                screenCode, request.IsAutomatic, request.Format, request.Prefix, request.SequenceLength,
                request.IsAttachmentMandatory, request.IsDescriptionMandatory),
            cancellationToken);

        return NoContent();
    }
}
