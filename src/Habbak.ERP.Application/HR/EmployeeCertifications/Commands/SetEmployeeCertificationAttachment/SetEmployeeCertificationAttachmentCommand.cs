using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeCertifications.Commands.SetEmployeeCertificationAttachment;

/// <summary>Docs/Implementation/Phase-3C-Research.md §3.3 — same narrow single-column command as
/// SetEmploymentContractAttachmentCommand, for the same reason (no Update UI risking stale-field
/// overwrites, §1.6).</summary>
public sealed record SetEmployeeCertificationAttachmentCommand(long Id, long? AttachmentId) : IRequest;

public sealed class SetEmployeeCertificationAttachmentCommandValidator : AbstractValidator<SetEmployeeCertificationAttachmentCommand>
{
    public SetEmployeeCertificationAttachmentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.AttachmentId).GreaterThan(0).When(x => x.AttachmentId.HasValue);
    }
}

public sealed class SetEmployeeCertificationAttachmentCommandHandler(IApplicationDbContext db)
    : IRequestHandler<SetEmployeeCertificationAttachmentCommand>
{
    public async Task Handle(SetEmployeeCertificationAttachmentCommand request, CancellationToken cancellationToken)
    {
        var certification = await db.EmployeeCertifications.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeCertification), request.Id);

        if (request.AttachmentId is { } attachmentId && !await db.Attachments.AnyAsync(a => a.Id == attachmentId, cancellationToken))
        {
            throw new NotFoundException(nameof(Attachment), attachmentId);
        }

        certification.AttachmentId = request.AttachmentId;
        await db.SaveChangesAsync(cancellationToken);
    }
}
