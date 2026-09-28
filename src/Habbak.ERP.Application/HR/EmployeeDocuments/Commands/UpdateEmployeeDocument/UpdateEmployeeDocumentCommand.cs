using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeDocuments.Commands.UpdateEmployeeDocument;

public sealed record UpdateEmployeeDocumentCommand(
    long Id, long EmployeeDocumentTypeId, DateOnly IssueDate, DateOnly? ExpiryDate, long AttachmentId, string? DocumentNumber) : IRequest;

public sealed class UpdateEmployeeDocumentCommandValidator : AbstractValidator<UpdateEmployeeDocumentCommand>
{
    public UpdateEmployeeDocumentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.EmployeeDocumentTypeId).GreaterThan(0);
        RuleFor(x => x.AttachmentId).GreaterThan(0);
        RuleFor(x => x.DocumentNumber).MaximumLength(100);
        RuleFor(x => x.ExpiryDate).GreaterThan(x => x.IssueDate).When(x => x.ExpiryDate.HasValue)
            .WithMessage("تاريخ الانتهاء لازم يكون بعد تاريخ الإصدار.");
    }
}

public sealed class UpdateEmployeeDocumentCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateEmployeeDocumentCommand>
{
    public async Task Handle(UpdateEmployeeDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await db.EmployeeDocuments.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeDocument), request.Id);

        var documentType = await db.EmployeeDocumentTypes.FirstOrDefaultAsync(t => t.Id == request.EmployeeDocumentTypeId, cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeDocumentType), request.EmployeeDocumentTypeId);

        if (documentType.RequiresExpiry && request.ExpiryDate is null)
        {
            throw new BusinessRuleException("HR-DOCUMENT-EXPIRY-REQUIRED", "نوع المستند ده لازم له تاريخ انتهاء.");
        }

        if (!await db.Attachments.AnyAsync(a => a.Id == request.AttachmentId, cancellationToken))
        {
            throw new NotFoundException(nameof(Attachment), request.AttachmentId);
        }

        document.EmployeeDocumentTypeId = request.EmployeeDocumentTypeId;
        document.IssueDate = request.IssueDate;
        document.ExpiryDate = request.ExpiryDate;
        document.AttachmentId = request.AttachmentId;
        document.DocumentNumber = request.DocumentNumber;

        await db.SaveChangesAsync(cancellationToken);
    }
}
