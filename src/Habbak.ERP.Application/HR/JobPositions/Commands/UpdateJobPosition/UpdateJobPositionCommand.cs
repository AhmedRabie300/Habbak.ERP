using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.JobPositions.Commands.UpdateJobPosition;

public sealed record UpdateJobPositionCommand(long Id, string NameAr, string NameEn, long? OrgUnitId, long? DefaultJobGradeId, bool IsActive) : IRequest;

public sealed class UpdateJobPositionCommandValidator : AbstractValidator<UpdateJobPositionCommand>
{
    public UpdateJobPositionCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateJobPositionCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdateJobPositionCommand>
{
    public async Task Handle(UpdateJobPositionCommand request, CancellationToken cancellationToken)
    {
        var position = await db.JobPositions.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(JobPosition), request.Id);

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

        position.NameAr = request.NameAr;
        position.NameEn = request.NameEn;
        position.OrgUnitId = request.OrgUnitId;
        position.DefaultJobGradeId = request.DefaultJobGradeId;
        position.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
