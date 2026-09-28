using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.OrgUnits.Commands.UpdateOrgUnit;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B4. ParentId reassignment is cycle-checked with
/// OrgUnitHierarchy.WouldCreateCycle — the same entity-agnostic helper Employee.ManagerId uses
/// (Batch B2, UpdateEmployeeCommand) — this is that helper's original intended use (OrgUnit.cs doc).
/// </summary>
public sealed record UpdateOrgUnitCommand(long Id, string NameAr, string NameEn, long? ParentId, long? BranchId, long? ManagerEmployeeId, long? CostCenterDimensionValueId, bool IsActive) : IRequest;

public sealed class UpdateOrgUnitCommandValidator : AbstractValidator<UpdateOrgUnitCommand>
{
    public UpdateOrgUnitCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateOrgUnitCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdateOrgUnitCommand>
{
    public async Task Handle(UpdateOrgUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = await db.OrgUnits.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(OrgUnit), request.Id);

        if (request.ParentId is not null)
        {
            if (request.ParentId == request.Id)
            {
                throw new BusinessRuleException("HR-ORG-UNIT-SELF-PARENT", "لا يمكن أن تكون الوحدة التنظيمية أصلًا لنفسها.");
            }

            if (!await db.OrgUnits.AnyAsync(u => u.Id == request.ParentId && u.CompanyId == currentCompanyContext.CompanyId, cancellationToken))
            {
                throw new NotFoundException(nameof(OrgUnit), request.ParentId.Value);
            }

            var parentsById = await db.OrgUnits
                .Where(u => u.CompanyId == currentCompanyContext.CompanyId)
                .Select(u => new { u.Id, u.ParentId })
                .ToDictionaryAsync(u => u.Id, u => u.ParentId, cancellationToken);

            if (OrgUnitHierarchy.WouldCreateCycle(request.Id, request.ParentId, parentsById))
            {
                throw new BusinessRuleException("HR-ORG-UNIT-PARENT-CYCLE", "هذا التعيين هيعمل حلقة في الهيكل التنظيمي.");
            }
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

        unit.NameAr = request.NameAr;
        unit.NameEn = request.NameEn;
        unit.ParentId = request.ParentId;
        unit.BranchId = request.BranchId;
        unit.ManagerEmployeeId = request.ManagerEmployeeId;
        unit.CostCenterDimensionValueId = request.CostCenterDimensionValueId;
        unit.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
