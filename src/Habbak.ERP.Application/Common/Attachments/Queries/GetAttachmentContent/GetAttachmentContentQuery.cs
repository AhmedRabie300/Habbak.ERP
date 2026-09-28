using Habbak.ERP.Application.Common.Attachments.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Common.Attachments.Queries.GetAttachmentContent;

public sealed record GetAttachmentContentQuery(long Id) : IRequest<AttachmentContentDto>;

public sealed class GetAttachmentContentQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAttachmentContentQuery, AttachmentContentDto>
{
    public async Task<AttachmentContentDto> Handle(GetAttachmentContentQuery request, CancellationToken cancellationToken)
    {
        var attachment = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Attachment), request.Id);

        return new AttachmentContentDto
        {
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            Content = attachment.Content
        };
    }
}
