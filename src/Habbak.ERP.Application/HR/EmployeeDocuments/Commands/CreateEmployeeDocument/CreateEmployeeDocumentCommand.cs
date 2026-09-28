using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeDocuments.Commands.CreateEmployeeDocument;

/// <summary>Docs/Implementation/HR-Core-Plan.md §1.1, Batch B6. BranchId mirrors the employee's own BranchId, same reasoning as CreateEmploymentContractCommand.</summary>
public sealed record CreateEmployeeDocumentCommand(
    long EmployeeId, long EmployeeDocumentTypeId, DateOnly IssueDate, DateOnly? ExpiryDate, long AttachmentId, string? DocumentNumber) : IRequest<long>;

public sealed class CreateEmployeeDocumentCommandValidator : AbstractValidator<CreateEmployeeDocumentCommand>
{
    public CreateEmployeeDocumentCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.EmployeeDocumentTypeId).GreaterThan(0);
        RuleFor(x => x.AttachmentId).GreaterThan(0);
        RuleFor(x => x.DocumentNumber).MaximumLength(100);
        RuleFor(x => x.ExpiryDate).GreaterThan(x => x.IssueDate).When(x => x.ExpiryDate.HasValue)
            .WithMessage("تاريخ الانتهاء لازم يكون بعد تاريخ الإصدار.");
    }
}

public sealed class CreateEmployeeDocumentCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<CreateEmployeeDocumentCommand, long>
{
    public async Task<long> Handle(CreateEmployeeDocumentCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

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

        var document = new EmployeeDocument
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = employee.BranchId,
            EmployeeId = request.EmployeeId,
            EmployeeDocumentTypeId = request.EmployeeDocumentTypeId,
            IssueDate = request.IssueDate,
            ExpiryDate = request.ExpiryDate,
            AttachmentId = request.AttachmentId,
            DocumentNumber = request.DocumentNumber
        };

        db.EmployeeDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        return document.Id;
    }
}
