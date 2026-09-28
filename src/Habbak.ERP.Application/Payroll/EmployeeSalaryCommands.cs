using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Payroll;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.6 — screen HR_SALARY_CHANGES (§5.1
// row 14, "مؤرّخة"). Filled manually by HR (Phase-4-Research.md §2.3 — no automatic conversion from
// EmploymentContractLine, and Hiring Wizard's own "راتب" step lands in Sub-Batch 4.7).

public sealed record EmployeeSalaryDto(long Id, long EmployeeId, long SalaryComponentId, decimal Amount, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public sealed record GetEmployeeSalariesQuery(long EmployeeId) : IRequest<IReadOnlyList<EmployeeSalaryDto>>;

public sealed class GetEmployeeSalariesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetEmployeeSalariesQuery, IReadOnlyList<EmployeeSalaryDto>>
{
    public async Task<IReadOnlyList<EmployeeSalaryDto>> Handle(GetEmployeeSalariesQuery request, CancellationToken cancellationToken) =>
        await db.EmployeeSalaries.AsNoTracking().Where(s => s.EmployeeId == request.EmployeeId)
            .OrderBy(s => s.SalaryComponentId).ThenByDescending(s => s.EffectiveFrom)
            .Select(s => new EmployeeSalaryDto(s.Id, s.EmployeeId, s.SalaryComponentId, s.Amount, s.EffectiveFrom, s.EffectiveTo))
            .ToListAsync(cancellationToken);
}

public sealed record CreateEmployeeSalaryCommand(long EmployeeId, long SalaryComponentId, decimal Amount, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<long>;

public sealed class CreateEmployeeSalaryCommandValidator : AbstractValidator<CreateEmployeeSalaryCommand>
{
    public CreateEmployeeSalaryCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.SalaryComponentId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateEmployeeSalaryCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current) : IRequestHandler<CreateEmployeeSalaryCommand, long>
{
    public async Task<long> Handle(CreateEmployeeSalaryCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        var sameKey = await db.EmployeeSalaries
            .Where(s => s.EmployeeId == request.EmployeeId && s.SalaryComponentId == request.SalaryComponentId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo);

        var entity = new EmployeeSalary
        {
            CompanyId = current.CompanyId, EmployeeId = request.EmployeeId, SalaryComponentId = request.SalaryComponentId,
            Amount = request.Amount, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo
        };
        db.EmployeeSalaries.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateEmployeeSalaryCommand(long Id, decimal Amount, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest;

public sealed class UpdateEmployeeSalaryCommandValidator : AbstractValidator<UpdateEmployeeSalaryCommand>
{
    public UpdateEmployeeSalaryCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateEmployeeSalaryCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateEmployeeSalaryCommand>
{
    public async Task Handle(UpdateEmployeeSalaryCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.EmployeeSalaries.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeSalary), request.Id);

        var sameKey = await db.EmployeeSalaries
            .Where(s => s.EmployeeId == entity.EmployeeId && s.SalaryComponentId == entity.SalaryComponentId).ToListAsync(cancellationToken);
        LegalTableRules.EnsureNoOverlap(sameKey, request.EffectiveFrom, request.EffectiveTo, request.Id);

        entity.Amount = request.Amount;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record DeleteEmployeeSalaryCommand(long Id) : IRequest;

public sealed class DeleteEmployeeSalaryCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteEmployeeSalaryCommand>
{
    public async Task Handle(DeleteEmployeeSalaryCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.EmployeeSalaries.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeSalary), request.Id);
        db.EmployeeSalaries.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
