using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Commands.UpdateEmployee;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B2. ManagerId reassignment is cycle-checked with
/// the same OrgUnitHierarchy.WouldCreateCycle helper OrgUnit.ParentId uses (rule 3,
/// Docs/Modules/10-Module-HR-Payroll.md §3.1) — the algorithm is entity-agnostic (any Id→ParentId
/// map), so nothing new was written for Employee specifically.
/// </summary>
public sealed record UpdateEmployeeCommand(
    long Id,
    string NameAr,
    string NameEn,
    long BranchId,
    long OrgUnitId,
    long JobPositionId,
    long JobGradeId,
    long? ManagerId,
    long? UserId,
    DateOnly HireDate,
    EmploymentType EmploymentType,
    long? CostCenterDimensionValueId,
    bool IsActive) : IRequest;

public sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.OrgUnitId).GreaterThan(0);
        RuleFor(x => x.JobPositionId).GreaterThan(0);
        RuleFor(x => x.JobGradeId).GreaterThan(0);
    }
}

public sealed class UpdateEmployeeCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdateEmployeeCommand>
{
    public async Task Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.Id);

        if (request.ManagerId is not null)
        {
            if (request.ManagerId == request.Id)
            {
                throw new BusinessRuleException("HR-EMPLOYEE-SELF-MANAGER", "لا يمكن أن يكون الموظف مديرًا لنفسه.");
            }

            if (!await db.Employees.AnyAsync(e => e.Id == request.ManagerId, cancellationToken))
            {
                throw new NotFoundException(nameof(Employee), request.ManagerId.Value);
            }

            var parentsById = await db.Employees
                .Where(e => e.CompanyId == currentCompanyContext.CompanyId)
                .Select(e => new { e.Id, e.ManagerId })
                .ToDictionaryAsync(e => e.Id, e => e.ManagerId, cancellationToken);

            if (OrgUnitHierarchy.WouldCreateCycle(request.Id, request.ManagerId, parentsById))
            {
                throw new BusinessRuleException("HR-EMPLOYEE-MANAGER-CYCLE", "هذا التعيين هيعمل حلقة في السلسلة الإدارية.");
            }
        }

        if (request.UserId is not null)
        {
            var userAlreadyLinked = await db.Employees.AnyAsync(
                e => e.Id != request.Id && e.CompanyId == currentCompanyContext.CompanyId && e.UserId == request.UserId,
                cancellationToken);
            if (userAlreadyLinked)
            {
                throw new BusinessRuleException("HR-USER-ALREADY-LINKED", "هذا المستخدم مربوط بموظف آخر بالفعل في هذه الشركة.");
            }
        }

        employee.NameAr = request.NameAr;
        employee.NameEn = request.NameEn;
        employee.BranchId = request.BranchId;
        employee.OrgUnitId = request.OrgUnitId;
        employee.JobPositionId = request.JobPositionId;
        employee.JobGradeId = request.JobGradeId;
        employee.ManagerId = request.ManagerId;
        employee.UserId = request.UserId;
        employee.HireDate = request.HireDate;
        employee.EmploymentType = request.EmploymentType;
        employee.CostCenterDimensionValueId = request.CostCenterDimensionValueId;
        employee.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
