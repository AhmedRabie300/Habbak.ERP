using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.Holidays.Commands;

/// <summary>Remarks8 Item 8 — EndDate = null يعني عطلة يوم واحد (StartDate بس). Year مبني على
/// StartDate (سنة بداية العطلة، حتى لو امتدت لسنة تانية — حالة نادرة).</summary>
public sealed record CreateHolidayCommand(string? Code, string NameAr, string NameEn, DateOnly StartDate, DateOnly? EndDate, bool IsNational, long? BranchId) : IRequest<long>;

public sealed class CreateHolidayCommandValidator : AbstractValidator<CreateHolidayCommand>
{
    public CreateHolidayCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate is not null);
    }
}

public sealed class CreateHolidayCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateHolidayCommand, long>
{
    public async Task<long> Handle(CreateHolidayCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("HR_HOLIDAYS", request.Code, cancellationToken);
        if (await db.Holidays.AnyAsync(h => h.CompanyId == current.CompanyId && h.Code == code, cancellationToken))
        {
            throw new BusinessRuleException("HR-HOLIDAY-CODE-EXISTS", "توجد عطلة أخرى بنفس الكود بالفعل.");
        }

        var entity = new Holiday
        {
            CompanyId = current.CompanyId,
            BranchId = request.BranchId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            IsActive = true,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Year = request.StartDate.Year,
            IsNational = request.IsNational
        };

        db.Holidays.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateHolidayCommand(long Id, string NameAr, string NameEn, DateOnly StartDate, DateOnly? EndDate, bool IsNational, long? BranchId, bool IsActive) : IRequest;

public sealed class UpdateHolidayCommandValidator : AbstractValidator<UpdateHolidayCommand>
{
    public UpdateHolidayCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate is not null);
    }
}

public sealed class UpdateHolidayCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateHolidayCommand>
{
    public async Task Handle(UpdateHolidayCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Holidays.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Holiday), request.Id);

        entity.NameAr = request.NameAr;
        entity.NameEn = request.NameEn;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.Year = request.StartDate.Year;
        entity.IsNational = request.IsNational;
        entity.BranchId = request.BranchId;
        entity.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record DeleteHolidayCommand(long Id) : IRequest;

public sealed class DeleteHolidayCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteHolidayCommand>
{
    public async Task Handle(DeleteHolidayCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Holidays.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Holiday), request.Id);

        db.Holidays.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
