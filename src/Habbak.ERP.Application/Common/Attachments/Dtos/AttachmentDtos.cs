namespace Habbak.ERP.Application.Common.Attachments.Dtos;

public sealed class AttachmentListItemDto
{
    public required long Id { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSizeBytes { get; init; }

    /// <summary>Sourced from AuditableEntity.CreatedAtUtc — never set independently.</summary>
    public required DateTime UploadedAtUtc { get; init; }

    /// <summary>Sourced from AuditableEntity.CreatedBy — never set independently.</summary>
    public required long UploadedByUserId { get; init; }
}

public sealed class AttachmentContentDto
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Content { get; init; }
}
