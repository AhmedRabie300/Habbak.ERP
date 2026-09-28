using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Commands.AssignUserToEmployee;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B5 — a focused, single-purpose action alongside
/// the general UpdateEmployeeCommand (Batch B2, which already accepts UserId too) for a dedicated
/// "link login account" UI step. Reuses rule 1 (10-Module-HR-Payroll.md §3.1): one user per employee,
/// per company — same HR-USER-ALREADY-LINKED check as UpdateEmployeeCommand.
/// </summary>
public sealed record AssignUserToEmployeeCommand(long EmployeeId, long UserId) : IRequest;

public sealed class AssignUserToEmployeeCommandValidator : AbstractValidator<AssignUserToEmployeeCommand>
{
    public AssignUserToEmployeeCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}

public sealed class AssignUserToEmployeeCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<AssignUserToEmployeeCommand>
{
    public async Task Handle(AssignUserToEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FindAsync([request.EmployeeId], cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        if (!await db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken))
        {
            throw new NotFoundException(nameof(User), request.UserId);
        }

        var userAlreadyLinked = await db.Employees.AnyAsync(
            e => e.Id != request.EmployeeId && e.CompanyId == currentCompanyContext.CompanyId && e.UserId == request.UserId,
            cancellationToken);
        if (userAlreadyLinked)
        {
            throw new BusinessRuleException("HR-USER-ALREADY-LINKED", "هذا المستخدم مربوط بموظف آخر بالفعل في هذه الشركة.");
        }

        employee.UserId = request.UserId;
        await db.SaveChangesAsync(cancellationToken);
    }
}
