using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Payroll;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.6 — screen PAY_SALARY_STRUCTURES.
// A template employees' EmployeeSalary rows get copied from at hiring (§2.3 module doc note) — this
// command set only manages the template itself, never touches EmployeeSalary.

public sealed record SalaryStructureLineDto(long Id, long SalaryComponentId, decimal? Amount, decimal? Percentage, int Order);
public sealed record SalaryStructureDto(long Id, string Code, string NameAr, string NameEn, bool IsActive, IReadOnlyList<SalaryStructureLineDto> Lines);

public sealed record GetSalaryStructuresListQuery : IRequest<IReadOnlyList<SalaryStructureDto>>;

public sealed class GetSalaryStructuresListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSalaryStructuresListQuery, IReadOnlyList<SalaryStructureDto>>
{
    public async Task<IReadOnlyList<SalaryStructureDto>> Handle(GetSalaryStructuresListQuery request, CancellationToken cancellationToken) =>
        await db.SalaryStructures.AsNoTracking().Include(s => s.Lines).OrderBy(s => s.Code)
            .Select(s => new SalaryStructureDto(s.Id, s.Code, s.NameAr, s.NameEn, s.IsActive,
                s.Lines.OrderBy(l => l.Order).Select(l => new SalaryStructureLineDto(l.Id, l.SalaryComponentId, l.Amount, l.Percentage, l.Order)).ToList()))
            .ToListAsync(cancellationToken);
}

public sealed record SalaryStructureLineInput(long SalaryComponentId, decimal? Amount, decimal? Percentage, int Order);

public sealed record CreateSalaryStructureCommand(string? Code, string NameAr, string NameEn, IReadOnlyList<SalaryStructureLineInput> Lines) : IRequest<long>;

public sealed class CreateSalaryStructureCommandValidator : AbstractValidator<CreateSalaryStructureCommand>
{
    public CreateSalaryStructureCommandValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateSalaryStructureCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateSalaryStructureCommand, long>
{
    public async Task<long> Handle(CreateSalaryStructureCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("PAY_SALARY_STRUCTURES", request.Code, cancellationToken);
        if (await db.SalaryStructures.AnyAsync(s => s.CompanyId == current.CompanyId && s.Code == code, cancellationToken))
        {
            throw new BusinessRuleException("PAY-SALARY-STRUCTURE-CODE-EXISTS", "يوجد هيكل راتب آخر بنفس الكود بالفعل.");
        }

        var entity = new SalaryStructure
        {
            CompanyId = current.CompanyId, Code = code, NameAr = request.NameAr, NameEn = request.NameEn, IsActive = true,
            Lines = request.Lines.Select(l => new SalaryStructureLine { SalaryComponentId = l.SalaryComponentId, Amount = l.Amount, Percentage = l.Percentage, Order = l.Order }).ToList()
        };
        db.SalaryStructures.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateSalaryStructureCommand(long Id, string NameAr, string NameEn, bool IsActive, IReadOnlyList<SalaryStructureLineInput> Lines) : IRequest;

public sealed class UpdateSalaryStructureCommandValidator : AbstractValidator<UpdateSalaryStructureCommand>
{
    public UpdateSalaryStructureCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateSalaryStructureCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateSalaryStructureCommand>
{
    public async Task Handle(UpdateSalaryStructureCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.SalaryStructures.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalaryStructure), request.Id);

        entity.NameAr = request.NameAr;
        entity.NameEn = request.NameEn;
        entity.IsActive = request.IsActive;

        entity.Lines.Clear();
        foreach (var line in request.Lines)
        {
            entity.Lines.Add(new SalaryStructureLine { SalaryComponentId = line.SalaryComponentId, Amount = line.Amount, Percentage = line.Percentage, Order = line.Order });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
