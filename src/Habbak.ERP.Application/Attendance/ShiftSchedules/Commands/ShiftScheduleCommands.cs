using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.ShiftSchedules.Commands;

/// <summary>Remarks8 Item 5 — StartDate/EndDate بقت فترة (EndDate = null يعني يوم واحد). التحقق
/// من عدم التداخل بقى مسؤولية الـ Handler (مفيش فهرس فريد على فترات).</summary>
public sealed record CreateShiftScheduleCommand(long EmployeeId, DateOnly StartDate, DateOnly? EndDate, long? WorkShiftDefinitionId, bool IsRestDay) : IRequest<long>;

public sealed class CreateShiftScheduleCommandValidator : AbstractValidator<CreateShiftScheduleCommand>
{
    public CreateShiftScheduleCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate is not null);
    }
}

public sealed class CreateShiftScheduleCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateShiftScheduleCommand, long>
{
    public async Task<long> Handle(CreateShiftScheduleCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        await EnsureNoOverlapAsync(db, request.EmployeeId, request.StartDate, request.EndDate, excludeId: null, cancellationToken);

        var entity = new ShiftSchedule
        {
            CompanyId = employee.CompanyId,
            BranchId = employee.BranchId,
            EmployeeId = request.EmployeeId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            WorkShiftDefinitionId = request.WorkShiftDefinitionId,
            IsRestDay = request.IsRestDay
        };

        db.ShiftSchedules.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    internal static async Task EnsureNoOverlapAsync(
        IApplicationDbContext db, long employeeId, DateOnly startDate, DateOnly? endDate, long? excludeId, CancellationToken cancellationToken)
    {
        var candidates = await db.ShiftSchedules
            .Where(s => s.EmployeeId == employeeId && s.Id != (excludeId ?? 0))
            .Select(s => new { s.Id, s.StartDate, s.EndDate })
            .ToListAsync(cancellationToken);

        var overlaps = candidates.Any(c => ShiftScheduleLookup.RangesOverlap(startDate, endDate, c.StartDate, c.EndDate));
        if (overlaps)
        {
            throw new BusinessRuleException("HR-SHIFT-SCHEDULE-OVERLAPS", "الفترة دي بتتداخل مع جدول وردية موجود بالفعل لنفس الموظف.");
        }
    }
}

public sealed record UpdateShiftScheduleCommand(long Id, DateOnly StartDate, DateOnly? EndDate, long? WorkShiftDefinitionId, bool IsRestDay) : IRequest;

public sealed class UpdateShiftScheduleCommandValidator : AbstractValidator<UpdateShiftScheduleCommand>
{
    public UpdateShiftScheduleCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate is not null);
    }
}

public sealed class UpdateShiftScheduleCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateShiftScheduleCommand>
{
    public async Task Handle(UpdateShiftScheduleCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.ShiftSchedules.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(ShiftSchedule), request.Id);

        await CreateShiftScheduleCommandHandler.EnsureNoOverlapAsync(db, entity.EmployeeId, request.StartDate, request.EndDate, excludeId: entity.Id, cancellationToken);

        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.WorkShiftDefinitionId = request.WorkShiftDefinitionId;
        entity.IsRestDay = request.IsRestDay;

        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record DeleteShiftScheduleCommand(long Id) : IRequest;

public sealed class DeleteShiftScheduleCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteShiftScheduleCommand>
{
    public async Task Handle(DeleteShiftScheduleCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.ShiftSchedules.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ShiftSchedule), request.Id);

        db.ShiftSchedules.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>بيبني جدول أسبوع كامل لموظف واحد — الأيام اللي ليها صف بالفعل بتتخطّى (Idempotent، مش
/// استبدال). كل يوم صف مستقل (StartDate = اليوم، EndDate = null) — البساطة هنا مقصودة، عكس
/// المولّد الجماعي (GenerateBulkShiftSchedulesCommand) اللي بيغطّي فترة/هيكل تنظيمي كامل.</summary>
public sealed record GenerateWeeklyScheduleCommand(long EmployeeId, DateOnly WeekStartDate, long? WorkShiftDefinitionId, DayOfWeek? RestDayOfWeek) : IRequest<int>;

public sealed class GenerateWeeklyScheduleCommandValidator : AbstractValidator<GenerateWeeklyScheduleCommand>
{
    public GenerateWeeklyScheduleCommandValidator() => RuleFor(x => x.EmployeeId).GreaterThan(0);
}

public sealed class GenerateWeeklyScheduleCommandHandler(IApplicationDbContext db) : IRequestHandler<GenerateWeeklyScheduleCommand, int>
{
    public async Task<int> Handle(GenerateWeeklyScheduleCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        var weekDates = Enumerable.Range(0, 7).Select(request.WeekStartDate.AddDays).ToList();
        var existing = await ShiftScheduleLookup.FindOverlappingAsync(db, request.EmployeeId, weekDates[0], weekDates[^1], cancellationToken);

        var created = 0;
        foreach (var date in weekDates)
        {
            if (existing.Any(s => s.StartDate <= date && (s.EndDate ?? s.StartDate) >= date))
            {
                continue;
            }

            db.ShiftSchedules.Add(new ShiftSchedule
            {
                CompanyId = employee.CompanyId,
                BranchId = employee.BranchId,
                EmployeeId = request.EmployeeId,
                StartDate = date,
                EndDate = null,
                WorkShiftDefinitionId = request.WorkShiftDefinitionId,
                IsRestDay = request.RestDayOfWeek is not null && date.DayOfWeek == request.RestDayOfWeek
            });
            created++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return created;
    }
}
