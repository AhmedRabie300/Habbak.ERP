using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Payroll;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.6 — screen PAY_SALARY_COMPONENTS.
// Same Lookup CRUD shape as WorkShiftDefinition (Attendance/WorkShiftDefinitions/Commands), no Delete
// (a component referenced by past PayrollLine/EmployeeSalary rows must stay resolvable — Deactivate
// via Update's IsActive is the only removal path, same convention as every ILookupEntity here).

public sealed record SalaryComponentDto(
    long Id, string Code, string NameAr, string NameEn, bool IsActive, SalaryComponentType ComponentType, CalculationMethod CalculationMethod,
    bool IsTaxable, bool IsInsurable, bool IsRecurring, SalaryComponentSource SourceType, CompanyAccountRole? AccountRole);

public sealed record GetSalaryComponentsListQuery : IRequest<IReadOnlyList<SalaryComponentDto>>;

public sealed class GetSalaryComponentsListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSalaryComponentsListQuery, IReadOnlyList<SalaryComponentDto>>
{
    public async Task<IReadOnlyList<SalaryComponentDto>> Handle(GetSalaryComponentsListQuery request, CancellationToken cancellationToken) =>
        await db.SalaryComponents.AsNoTracking().OrderBy(c => c.Code)
            .Select(c => new SalaryComponentDto(
                c.Id, c.Code, c.NameAr, c.NameEn, c.IsActive, c.ComponentType, c.CalculationMethod, c.IsTaxable, c.IsInsurable, c.IsRecurring, c.SourceType, c.AccountRole))
            .ToListAsync(cancellationToken);
}

public sealed record CreateSalaryComponentCommand(
    string? Code, string NameAr, string NameEn, SalaryComponentType ComponentType, CalculationMethod CalculationMethod,
    bool IsTaxable, bool IsInsurable, bool IsRecurring, SalaryComponentSource SourceType, CompanyAccountRole? AccountRole) : IRequest<long>;

public sealed class CreateSalaryComponentCommandValidator : AbstractValidator<CreateSalaryComponentCommand>
{
    public CreateSalaryComponentCommandValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateSalaryComponentCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateSalaryComponentCommand, long>
{
    public async Task<long> Handle(CreateSalaryComponentCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("PAY_SALARY_COMPONENTS", request.Code, cancellationToken);

        if (await db.SalaryComponents.AnyAsync(c => c.CompanyId == current.CompanyId && c.Code == code, cancellationToken))
        {
            throw new BusinessRuleException("PAY-SALARY-COMPONENT-CODE-EXISTS", "يوجد بند راتب آخر بنفس الكود بالفعل.");
        }

        var entity = new SalaryComponent
        {
            CompanyId = current.CompanyId, Code = code, NameAr = request.NameAr, NameEn = request.NameEn, IsActive = true,
            ComponentType = request.ComponentType, CalculationMethod = request.CalculationMethod, IsTaxable = request.IsTaxable,
            IsInsurable = request.IsInsurable, IsRecurring = request.IsRecurring, SourceType = request.SourceType, AccountRole = request.AccountRole
        };
        db.SalaryComponents.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateSalaryComponentCommand(
    long Id, string NameAr, string NameEn, bool IsActive, SalaryComponentType ComponentType, CalculationMethod CalculationMethod,
    bool IsTaxable, bool IsInsurable, bool IsRecurring, SalaryComponentSource SourceType, CompanyAccountRole? AccountRole) : IRequest;

public sealed class UpdateSalaryComponentCommandValidator : AbstractValidator<UpdateSalaryComponentCommand>
{
    public UpdateSalaryComponentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateSalaryComponentCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateSalaryComponentCommand>
{
    public async Task Handle(UpdateSalaryComponentCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.SalaryComponents.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(SalaryComponent), request.Id);

        entity.NameAr = request.NameAr;
        entity.NameEn = request.NameEn;
        entity.IsActive = request.IsActive;
        entity.ComponentType = request.ComponentType;
        entity.CalculationMethod = request.CalculationMethod;
        entity.IsTaxable = request.IsTaxable;
        entity.IsInsurable = request.IsInsurable;
        entity.IsRecurring = request.IsRecurring;
        entity.SourceType = request.SourceType;
        entity.AccountRole = request.AccountRole;
        await db.SaveChangesAsync(cancellationToken);
    }
}
