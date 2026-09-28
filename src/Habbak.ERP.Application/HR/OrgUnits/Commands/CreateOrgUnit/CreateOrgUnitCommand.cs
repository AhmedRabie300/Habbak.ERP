using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.OrgUnits.Commands.CreateOrgUnit;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B4. Company-scoped (OrgUnit.cs doc) — unique on
/// (CompanyId, Code). A brand-new OrgUnit cannot create a ParentId cycle (it has no Id yet to be its
/// own ancestor) — cycle detection only matters on Update, once the node might already have
/// descendants (see UpdateOrgUnitCommand).
/// </summary>
public sealed record CreateOrgUnitCommand(string? Code, string NameAr, string NameEn, long? ParentId, long? BranchId, long? ManagerEmployeeId, long? CostCenterDimensionValueId) : IRequest<long>;

public sealed class CreateOrgUnitCommandValidator : AbstractValidator<CreateOrgUnitCommand>
{
    public CreateOrgUnitCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateOrgUnitCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateOrgUnitCommand, long>
{
    public async Task<long> Handle(CreateOrgUnitCommand request, CancellationToken cancellationToken)
    {
        if (request.ParentId is not null &&
            !await db.OrgUnits.AnyAsync(u => u.Id == request.ParentId && u.CompanyId == currentCompanyContext.CompanyId, cancellationToken))
        {
            throw new NotFoundException(nameof(OrgUnit), request.ParentId.Value);
        }

        if (request.BranchId is not null &&
            !await db.Branches.AnyAsync(b => b.Id == request.BranchId && b.CompanyId == currentCompanyContext.CompanyId, cancellationToken))
        {
            throw new NotFoundException(nameof(Branch), request.BranchId.Value);
        }

        if (request.CostCenterDimensionValueId is not null &&
            !await db.CostCenterDimensionValues.AnyAsync(v => v.Id == request.CostCenterDimensionValueId, cancellationToken))
        {
            throw new NotFoundException(nameof(CostCenterDimensionValue), request.CostCenterDimensionValueId.Value);
        }

        var code = await codeGenerator.ResolveCodeAsync("HR_ORG_UNITS", request.Code, cancellationToken);

        var codeExists = await db.OrgUnits
            .AnyAsync(u => u.CompanyId == currentCompanyContext.CompanyId && u.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("HR-ORG-UNIT-CODE-EXISTS", "توجد وحدة تنظيمية أخرى بنفس الكود بالفعل.");
        }

        var unit = new OrgUnit
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            ParentId = request.ParentId,
            BranchId = request.BranchId,
            ManagerEmployeeId = request.ManagerEmployeeId,
            CostCenterDimensionValueId = request.CostCenterDimensionValueId,
            IsActive = true
        };

        db.OrgUnits.Add(unit);
        await db.SaveChangesAsync(cancellationToken);

        return unit.Id;
    }
}
