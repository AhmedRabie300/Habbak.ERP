using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Commands.CreateEmployee;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B2 — creates a bare Employee (Status=Draft).
/// EmployeePersonalData is a separate command (CreateEmployeePersonalDataCommand): the two tables
/// have independent field-permission scopes on purpose (rule, Docs/Modules/10-Module-HR-Payroll.md
/// §2.1), and the real onboarding sequencing (contract, documents, salary) is HR_HIRING's job (1.5,
/// out of scope here). No cycle check on ManagerId here — a brand-new employee cannot be an ancestor
/// of an already-existing one (see UpdateEmployeeCommand, where reassignment can create a cycle).
/// </summary>
public sealed record CreateEmployeeCommand(
    string? Code,
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
    long? CostCenterDimensionValueId) : IRequest<long>;

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.OrgUnitId).GreaterThan(0);
        RuleFor(x => x.JobPositionId).GreaterThan(0);
        RuleFor(x => x.JobGradeId).GreaterThan(0);
    }
}

public sealed class CreateEmployeeCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateEmployeeCommand, long>
{
    public async Task<long> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("HR_EMPLOYEES", request.Code, cancellationToken);

        var codeExists = await db.Employees
            .AnyAsync(e => e.CompanyId == currentCompanyContext.CompanyId && e.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("HR-EMPLOYEE-CODE-EXISTS", "يوجد موظف آخر بنفس الكود بالفعل.");
        }

        if (!await db.OrgUnits.AnyAsync(u => u.Id == request.OrgUnitId, cancellationToken))
        {
            throw new NotFoundException(nameof(OrgUnit), request.OrgUnitId);
        }

        if (!await db.JobPositions.AnyAsync(p => p.Id == request.JobPositionId, cancellationToken))
        {
            throw new NotFoundException(nameof(JobPosition), request.JobPositionId);
        }

        if (!await db.JobGrades.AnyAsync(g => g.Id == request.JobGradeId, cancellationToken))
        {
            throw new NotFoundException(nameof(JobGrade), request.JobGradeId);
        }

        if (request.ManagerId is not null && !await db.Employees.AnyAsync(e => e.Id == request.ManagerId, cancellationToken))
        {
            throw new NotFoundException(nameof(Employee), request.ManagerId.Value);
        }

        if (request.UserId is not null)
        {
            var userAlreadyLinked = await db.Employees.AnyAsync(
                e => e.CompanyId == currentCompanyContext.CompanyId && e.UserId == request.UserId, cancellationToken);
            if (userAlreadyLinked)
            {
                throw new BusinessRuleException("HR-USER-ALREADY-LINKED", "هذا المستخدم مربوط بموظف آخر بالفعل في هذه الشركة.");
            }
        }

        var employee = new Employee
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            IsActive = true,
            BranchId = request.BranchId,
            OrgUnitId = request.OrgUnitId,
            JobPositionId = request.JobPositionId,
            JobGradeId = request.JobGradeId,
            ManagerId = request.ManagerId,
            UserId = request.UserId,
            HireDate = request.HireDate,
            EmploymentType = request.EmploymentType,
            Status = EmployeeStatus.Draft,
            CostCenterDimensionValueId = request.CostCenterDimensionValueId
        };

        db.Employees.Add(employee);
        await db.SaveChangesAsync(cancellationToken);

        return employee.Id;
    }
}
