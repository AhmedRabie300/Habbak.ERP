using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.JobGrades.Commands.CreateJobGrade;

/// <summary>Docs/Implementation/HR-Core-Plan.md §1.1, Batch B4. Company-scoped (JobGrade.cs doc) — unique on (CompanyId, Code).</summary>
public sealed record CreateJobGradeCommand(string? Code, string NameAr, string NameEn, int Level, decimal? MinSalary, decimal? MaxSalary) : IRequest<long>;

public sealed class CreateJobGradeCommandValidator : AbstractValidator<CreateJobGradeCommand>
{
    public CreateJobGradeCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MinSalary).GreaterThanOrEqualTo(0).When(x => x.MinSalary.HasValue);
        RuleFor(x => x.MaxSalary).GreaterThanOrEqualTo(0).When(x => x.MaxSalary.HasValue);
        RuleFor(x => x).Must(x => !x.MinSalary.HasValue || !x.MaxSalary.HasValue || x.MaxSalary >= x.MinSalary)
            .WithMessage("الحد الأقصى للراتب لازم يكون أكبر من أو يساوي الحد الأدنى.");
    }
}

public sealed class CreateJobGradeCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateJobGradeCommand, long>
{
    public async Task<long> Handle(CreateJobGradeCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("HR_JOB_GRADES", request.Code, cancellationToken);

        var codeExists = await db.JobGrades
            .AnyAsync(g => g.CompanyId == currentCompanyContext.CompanyId && g.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("HR-JOB-GRADE-CODE-EXISTS", "توجد درجة وظيفية أخرى بنفس الكود بالفعل.");
        }

        var grade = new JobGrade
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            Level = request.Level,
            MinSalary = request.MinSalary,
            MaxSalary = request.MaxSalary,
            IsActive = true
        };

        db.JobGrades.Add(grade);
        await db.SaveChangesAsync(cancellationToken);

        return grade.Id;
    }
}
