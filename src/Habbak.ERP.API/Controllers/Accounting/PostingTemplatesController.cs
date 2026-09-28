using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Application.Posting.Templates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>/accounting/posting-templates — the posting engine's templates and the screen settings built on them.</summary>
[ApiController]
[Authorize]
[Screen("SETTINGS_CODING_RULES")]
[Route("api/v1/accounting/posting-templates")]
public class PostingTemplatesController(ISender mediator) : ControllerBase
{
    public sealed record CreatePostingTemplateRequest(string ScreenCode, PostingTemplateDefinition Definition, bool IsActive = true);

    public sealed record CreateDefaultTemplatesRequest(string ScreenCode);

    public sealed record SetScreenActiveRequest(bool IsActive);

    public sealed record PreviewRequest(string ScreenCode, PostingTemplateDefinition Definition, PostingPreviewSample? Sample);

    public sealed record PreviewScreenRequest(string ScreenCode, PostingPreviewSample? Sample);

    /// <summary>Every screen that posts, with its fields and templates.</summary>
    [HttpGet("screens")]
    public async Task<IActionResult> GetScreens(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPostingScreensQuery(), cancellationToken));

    /// <summary>"الشاشة بترحّل قيود؟" — switches all of a screen's templates on or off.</summary>
    [HttpPost("screens/{screenCode}/active")]
    public async Task<IActionResult> SetScreenActive(string screenCode, [FromBody] SetScreenActiveRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetPostingScreenActiveCommand(screenCode, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpPost("defaults")]
    public async Task<IActionResult> CreateDefaults([FromBody] CreateDefaultTemplatesRequest request, CancellationToken cancellationToken) =>
        Ok(new { ids = await mediator.Send(new CreateDefaultPostingTemplatesCommand(request.ScreenCode), cancellationToken) });

    /// <summary>The entry one template would post for a sample document. Saves nothing.</summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] PreviewRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new PreviewPostingTemplateQuery(request.ScreenCode, request.Definition, request.Sample), cancellationToken));

    /// <summary>Every entry the screen's templates would post for one sample document. Saves nothing.</summary>
    [HttpPost("preview-screen")]
    public async Task<IActionResult> PreviewScreen([FromBody] PreviewScreenRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new PreviewPostingScreenQuery(request.ScreenCode, request.Sample), cancellationToken));

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] string? screenCode, [FromQuery] bool includeHistory, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPostingTemplatesListQuery(screenCode, includeHistory), cancellationToken));

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPostingEngineCatalogQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPostingTemplateByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePostingTemplateRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePostingTemplateCommand(request.ScreenCode, request.Definition, request.IsActive), cancellationToken);
        return Ok(new { id });
    }

    /// <summary>Returns the id that now holds the template — a new one when a new version was made.</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] PostingTemplateDefinition definition, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new UpdatePostingTemplateCommand(id, definition), cancellationToken));

    [HttpPost("{id:long}/activate")]
    public async Task<IActionResult> Activate(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetPostingTemplateActiveCommand(id, true), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/deactivate")]
    public async Task<IActionResult> Deactivate(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetPostingTemplateActiveCommand(id, false), cancellationToken);
        return NoContent();
    }
}
