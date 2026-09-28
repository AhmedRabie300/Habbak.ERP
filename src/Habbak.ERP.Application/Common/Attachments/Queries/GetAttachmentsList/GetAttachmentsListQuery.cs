using Habbak.ERP.Application.Common.Attachments.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Common.Attachments.Queries.GetAttachmentsList;

public sealed record GetAttachmentsListQuery(string EntityType, long EntityId) : IRequest<IReadOnlyList<AttachmentListItemDto>>;

public sealed class GetAttachmentsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAttachmentsListQuery, IReadOnlyList<AttachmentListItemDto>>
{
    public async Task<IReadOnlyList<AttachmentListItemDto>> Handle(GetAttachmentsListQuery request, CancellationToken cancellationToken) =>
        await db.Attachments
            .AsNoTracking()
            .Where(a => a.EntityType == request.EntityType && a.EntityId == request.EntityId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new AttachmentListItemDto
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes,
                UploadedAtUtc = a.CreatedAtUtc,
                UploadedByUserId = a.CreatedBy
            })
            .ToListAsync(cancellationToken);
}
