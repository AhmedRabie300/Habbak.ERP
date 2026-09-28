using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.WorkShiftDefinitions.Commands;

public sealed record CreateWorkShiftDefinitionCommand(string? Code, string NameAr, string NameEn, TimeOnly StartTime, TimeOnly EndTime, int BreakMinutes, bool IsNightShift) : IRequest<long>;

public sealed class CreateWorkShiftDefinitionCommandValidator : AbstractValidator<CreateWorkShiftDefinitionCommand>
{
    public CreateWorkShiftDefinitionCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BreakMinutes).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateWorkShiftDefinitionCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateWorkShiftDefinitionCommand, long>
{
    public async Task<long> Handle(CreateWorkShiftDefinitionCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("HR_WORK_SHIFTS", request.Code, cancellationToken);

        if (await db.WorkShiftDefinitions.AnyAsync(w => w.CompanyId == current.CompanyId && w.Code == code, cancellationToken))
        {
            throw new BusinessRuleException("HR-WORK-SHIFT-CODE-EXISTS", "توجد وردية عمل أخرى بنفس الكود بالفعل.");
        }

        var entity = new WorkShiftDefinition
        {
            CompanyId = current.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            BreakMinutes = request.BreakMinutes,
            IsNightShift = request.IsNightShift,
            IsActive = true
        };

        db.WorkShiftDefinitions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateWorkShiftDefinitionCommand(long Id, string NameAr, string NameEn, TimeOnly StartTime, TimeOnly EndTime, int BreakMinutes, bool IsNightShift, bool IsActive) : IRequest;

public sealed class UpdateWorkShiftDefinitionCommandValidator : AbstractValidator<UpdateWorkShiftDefinitionCommand>
{
    public UpdateWorkShiftDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BreakMinutes).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateWorkShiftDefinitionCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateWorkShiftDefinitionCommand>
{
    public async Task Handle(UpdateWorkShiftDefinitionCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.WorkShiftDefinitions.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(WorkShiftDefinition), request.Id);

        entity.NameAr = request.NameAr;
        entity.NameEn = request.NameEn;
        entity.StartTime = request.StartTime;
        entity.EndTime = request.EndTime;
        entity.BreakMinutes = request.BreakMinutes;
        entity.IsNightShift = request.IsNightShift;
        entity.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record DeleteWorkShiftDefinitionCommand(long Id) : IRequest;

public sealed class DeleteWorkShiftDefinitionCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteWorkShiftDefinitionCommand>
{
    public async Task Handle(DeleteWorkShiftDefinitionCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.WorkShiftDefinitions.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WorkShiftDefinition), request.Id);

        db.WorkShiftDefinitions.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
