using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Common.Attachments.Commands.DeleteAttachment;

public sealed record DeleteAttachmentCommand(long Id) : IRequest;

public sealed class DeleteAttachmentCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteAttachmentCommand>
{
    public async Task Handle(DeleteAttachmentCommand request, CancellationToken cancellationToken)
    {
        var attachment = await db.Attachments.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Attachment), request.Id);

        db.Attachments.Remove(attachment);
        await db.SaveChangesAsync(cancellationToken);
    }
}
