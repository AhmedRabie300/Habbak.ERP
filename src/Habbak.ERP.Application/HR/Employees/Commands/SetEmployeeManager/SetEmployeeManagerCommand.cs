using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Commands.SetEmployeeManager;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B5 — a focused, single-purpose action (e.g. an
/// org-chart drag-drop UI) alongside the general UpdateEmployeeCommand (Batch B2, which already
/// accepts ManagerId too). Same cycle-check as UpdateEmployeeCommand: OrgUnitHierarchy.WouldCreateCycle,
/// the entity-agnostic helper OrgUnit.ParentId uses too (Batch B4, UpdateOrgUnitCommand).
/// </summary>
public sealed record SetEmployeeManagerCommand(long EmployeeId, long? ManagerId) : IRequest;

public sealed class SetEmployeeManagerCommandValidator : AbstractValidator<SetEmployeeManagerCommand>
{
    public SetEmployeeManagerCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
    }
}

public sealed class SetEmployeeManagerCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<SetEmployeeManagerCommand>
{
    public async Task Handle(SetEmployeeManagerCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FindAsync([request.EmployeeId], cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        if (request.ManagerId is not null)
        {
            if (request.ManagerId == request.EmployeeId)
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

            if (OrgUnitHierarchy.WouldCreateCycle(request.EmployeeId, request.ManagerId, parentsById))
            {
                throw new BusinessRuleException("HR-EMPLOYEE-MANAGER-CYCLE", "هذا التعيين هيعمل حلقة في السلسلة الإدارية.");
            }
        }

        employee.ManagerId = request.ManagerId;
        await db.SaveChangesAsync(cancellationToken);
    }
}
