using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.JobPositions.Commands.CreateJobPosition;

/// <summary>Docs/Implementation/HR-Core-Plan.md §1.1, Batch B4. Company-scoped (JobPosition.cs doc) — unique on (CompanyId, Code). OrgUnitId/DefaultJobGradeId are both nullable (JobPosition.cs) but, when given, must belong to the same company.</summary>
public sealed record CreateJobPositionCommand(string? Code, string NameAr, string NameEn, long? OrgUnitId, long? DefaultJobGradeId) : IRequest<long>;

public sealed class CreateJobPositionCommandValidator : AbstractValidator<CreateJobPositionCommand>
{
    public CreateJobPositionCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateJobPositionCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateJobPositionCommand, long>
{
    public async Task<long> Handle(CreateJobPositionCommand request, CancellationToken cancellationToken)
    {
        if (request.OrgUnitId is not null &&
            !await db.OrgUnits.AnyAsync(u => u.Id == request.OrgUnitId && u.CompanyId == currentCompanyContext.CompanyId, cancellationToken))
        {
            throw new NotFoundException(nameof(OrgUnit), request.OrgUnitId.Value);
        }

        if (request.DefaultJobGradeId is not null &&
            !await db.JobGrades.AnyAsync(g => g.Id == request.DefaultJobGradeId && g.CompanyId == currentCompanyContext.CompanyId, cancellationToken))
        {
            throw new NotFoundException(nameof(JobGrade), request.DefaultJobGradeId.Value);
        }

        var code = await codeGenerator.ResolveCodeAsync("HR_JOB_POSITIONS", request.Code, cancellationToken);

        var codeExists = await db.JobPositions
            .AnyAsync(p => p.CompanyId == currentCompanyContext.CompanyId && p.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("HR-JOB-POSITION-CODE-EXISTS", "توجد وظيفة أخرى بنفس الكود بالفعل.");
        }

        var position = new JobPosition
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            OrgUnitId = request.OrgUnitId,
            DefaultJobGradeId = request.DefaultJobGradeId,
            IsActive = true
        };

        db.JobPositions.Add(position);
        await db.SaveChangesAsync(cancellationToken);

        return position.Id;
    }
}
