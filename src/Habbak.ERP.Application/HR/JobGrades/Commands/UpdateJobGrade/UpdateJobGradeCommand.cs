using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;

namespace Habbak.ERP.Application.HR.JobGrades.Commands.UpdateJobGrade;

public sealed record UpdateJobGradeCommand(long Id, string NameAr, string NameEn, int Level, decimal? MinSalary, decimal? MaxSalary, bool IsActive) : IRequest;

public sealed class UpdateJobGradeCommandValidator : AbstractValidator<UpdateJobGradeCommand>
{
    public UpdateJobGradeCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MinSalary).GreaterThanOrEqualTo(0).When(x => x.MinSalary.HasValue);
        RuleFor(x => x.MaxSalary).GreaterThanOrEqualTo(0).When(x => x.MaxSalary.HasValue);
        RuleFor(x => x).Must(x => !x.MinSalary.HasValue || !x.MaxSalary.HasValue || x.MaxSalary >= x.MinSalary)
            .WithMessage("الحد الأقصى للراتب لازم يكون أكبر من أو يساوي الحد الأدنى.");
    }
}

public sealed class UpdateJobGradeCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateJobGradeCommand>
{
    public async Task Handle(UpdateJobGradeCommand request, CancellationToken cancellationToken)
    {
        var grade = await db.JobGrades.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(JobGrade), request.Id);

        grade.NameAr = request.NameAr;
        grade.NameEn = request.NameEn;
        grade.Level = request.Level;
        grade.MinSalary = request.MinSalary;
        grade.MaxSalary = request.MaxSalary;
        grade.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
