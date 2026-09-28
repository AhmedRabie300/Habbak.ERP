using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.CustodyOfficers.Commands.CreateCustodyOfficer;

public sealed record CreateCustodyOfficerCommand(string? Code, string NameAr, string NameEn, long? BranchId) : IRequest<long>;

public sealed class CreateCustodyOfficerCommandValidator : AbstractValidator<CreateCustodyOfficerCommand>
{
    public CreateCustodyOfficerCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateCustodyOfficerCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateCustodyOfficerCommand, long>
{
    public async Task<long> Handle(CreateCustodyOfficerCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("INVENTORY_CUSTODY_OFFICERS", request.Code, cancellationToken);

        var codeExists = await db.CustodyOfficers
            .AnyAsync(c => c.CompanyId == currentCompanyContext.CompanyId && c.Code == code, cancellationToken);

        if (codeExists)
        {
            throw new BusinessRuleException("INV-CUSTODY-OFFICER-CODE-EXISTS", "يوجد مسؤول عهدة آخر بنفس الكود بالفعل.");
        }

        var officer = new CustodyOfficer
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            BranchId = request.BranchId,
            IsActive = true
        };

        db.CustodyOfficers.Add(officer);
        await db.SaveChangesAsync(cancellationToken);

        return officer.Id;
    }
}
