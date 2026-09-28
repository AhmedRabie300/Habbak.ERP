using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeDocumentTypes.Commands.CreateEmployeeDocumentType;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B4. Company-scoped (EmployeeDocumentType.cs doc)
/// — unique on (CompanyId, Code). Screen code is HR_DOCUMENT_TYPES (registered in Batch B1's
/// MenuItemSeedData/ScreenCodeCatalog) — not HR_EMPLOYEE_DOCUMENT_TYPES.
/// </summary>
public sealed record CreateEmployeeDocumentTypeCommand(string? Code, string NameAr, string NameEn, bool RequiresExpiry, bool IsMandatory, int? ExpiryAlertDays) : IRequest<long>;

public sealed class CreateEmployeeDocumentTypeCommandValidator : AbstractValidator<CreateEmployeeDocumentTypeCommand>
{
    public CreateEmployeeDocumentTypeCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExpiryAlertDays).GreaterThanOrEqualTo(0).When(x => x.ExpiryAlertDays.HasValue);
    }
}

public sealed class CreateEmployeeDocumentTypeCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateEmployeeDocumentTypeCommand, long>
{
    public async Task<long> Handle(CreateEmployeeDocumentTypeCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("HR_DOCUMENT_TYPES", request.Code, cancellationToken);

        var codeExists = await db.EmployeeDocumentTypes
            .AnyAsync(t => t.CompanyId == currentCompanyContext.CompanyId && t.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("HR-DOCUMENT-TYPE-CODE-EXISTS", "يوجد نوع مستند آخر بنفس الكود بالفعل.");
        }

        var documentType = new EmployeeDocumentType
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            RequiresExpiry = request.RequiresExpiry,
            IsMandatory = request.IsMandatory,
            ExpiryAlertDays = request.ExpiryAlertDays,
            IsActive = true
        };

        db.EmployeeDocumentTypes.Add(documentType);
        await db.SaveChangesAsync(cancellationToken);

        return documentType.Id;
    }
}
