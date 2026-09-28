using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmploymentContracts.Commands.SetEmploymentContractAttachment;

/// <summary>
/// Docs/Implementation/Phase-3C-Research.md §3.3 — a narrow, single-column command instead of
/// reusing UpdateEmploymentContractCommand, so attaching a file to a contract that already exists
/// never risks overwriting the rest of its fields from a stale form in memory (§1.6).
/// </summary>
public sealed record SetEmploymentContractAttachmentCommand(long Id, long? AttachmentId) : IRequest;

public sealed class SetEmploymentContractAttachmentCommandValidator : AbstractValidator<SetEmploymentContractAttachmentCommand>
{
    public SetEmploymentContractAttachmentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.AttachmentId).GreaterThan(0).When(x => x.AttachmentId.HasValue);
    }
}

public sealed class SetEmploymentContractAttachmentCommandHandler(IApplicationDbContext db)
    : IRequestHandler<SetEmploymentContractAttachmentCommand>
{
    public async Task Handle(SetEmploymentContractAttachmentCommand request, CancellationToken cancellationToken)
    {
        var contract = await db.EmploymentContracts.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(EmploymentContract), request.Id);

        if (request.AttachmentId is { } attachmentId && !await db.Attachments.AnyAsync(a => a.Id == attachmentId, cancellationToken))
        {
            throw new NotFoundException(nameof(Attachment), attachmentId);
        }

        contract.AttachmentId = request.AttachmentId;
        await db.SaveChangesAsync(cancellationToken);
    }
}
