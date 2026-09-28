using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.InsuranceOffices.Commands.CreateInsuranceOffice;

/// <summary>Docs/Implementation/HR-Core-Plan.md §1.1, Batch B4. System-wide (InsuranceOffice.cs doc) — no CompanyId, unique on Code alone.</summary>
public sealed record CreateInsuranceOfficeCommand(string? Code, string NameAr, string NameEn, string? OfficialCode, string? Address) : IRequest<long>;

public sealed class CreateInsuranceOfficeCommandValidator : AbstractValidator<CreateInsuranceOfficeCommand>
{
    public CreateInsuranceOfficeCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OfficialCode).MaximumLength(50);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

public sealed class CreateInsuranceOfficeCommandHandler(IApplicationDbContext db, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateInsuranceOfficeCommand, long>
{
    public async Task<long> Handle(CreateInsuranceOfficeCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("HR_INSURANCE_OFFICES", request.Code, cancellationToken);

        var codeExists = await db.InsuranceOffices.AnyAsync(o => o.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("HR-INSURANCE-OFFICE-CODE-EXISTS", "يوجد مكتب تأمينات آخر بنفس الكود بالفعل.");
        }

        var office = new InsuranceOffice
        {
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            OfficialCode = request.OfficialCode,
            Address = request.Address,
            IsActive = true
        };

        db.InsuranceOffices.Add(office);
        await db.SaveChangesAsync(cancellationToken);

        return office.Id;
    }
}
