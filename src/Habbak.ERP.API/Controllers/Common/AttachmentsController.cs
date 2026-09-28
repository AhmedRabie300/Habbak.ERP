using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Common.Attachments.Commands.DeleteAttachment;
using Habbak.ERP.Application.Common.Attachments.Commands.UploadAttachment;
using Habbak.ERP.Application.Common.Attachments.Queries.GetAttachmentContent;
using Habbak.ERP.Application.Common.Attachments.Queries.GetAttachmentsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Common;

/// <summary>/attachments — My Remarks/Remarks2.md, remark 3.3: one generic mechanism reused by
/// every Edit/New screen's own "Attachments" action, rather than a parallel table per module.</summary>
[ApiController]
[Authorize]
[AnySignedInUser]
[Route("api/v1/attachments")]
public class AttachmentsController(ISender mediator) : ControllerBase
{
    /// <summary>10 MB — matches UploadAttachmentCommandValidator's own limit; rejected here first
    /// so an oversized upload never has to travel all the way to the validator.</summary>
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>Swashbuckle can't generate a schema when an IFormFile parameter sits alongside
    /// separate scalar [FromForm] parameters on the action itself — it requires every [FromForm]
    /// value bound through a single model class instead (see the Swashbuckle "Handle Forms and File
    /// Uploads" doc referenced in its own SwaggerGeneratorException). Kept as a private nested type
    /// since nothing outside this action needs it.</summary>
    public sealed class UploadAttachmentRequest
    {
        public string EntityType { get; set; } = null!;
        public long EntityId { get; set; }
        public IFormFile File { get; set; } = null!;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] string entityType, [FromQuery] long entityId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAttachmentsListQuery(entityType, entityId), cancellationToken));

    [HttpPost]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> Upload([FromForm] UploadAttachmentRequest request, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await request.File.CopyToAsync(stream, cancellationToken);

        var id = await mediator.Send(
            new UploadAttachmentCommand(request.EntityType, request.EntityId, request.File.FileName, request.File.ContentType, stream.ToArray()),
            cancellationToken);

        return Ok(new { id });
    }

    [HttpGet("{id:long}/content")]
    public async Task<IActionResult> GetContent(long id, CancellationToken cancellationToken)
    {
        var content = await mediator.Send(new GetAttachmentContentQuery(id), cancellationToken);
        return File(content.Content, content.ContentType, content.FileName);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteAttachmentCommand(id), cancellationToken);
        return NoContent();
    }
}
