using FluentValidation;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.ShiftSchedules.Commands;

public sealed class BulkGenerateResultDto
{
    public required int EmployeesProcessed { get; init; }
    public required int DaysCreated { get; init; }
}

/// <summary>
/// Remarks8 Item 7 — Screen HR_SHIFT_SCHEDULE_GENERATOR. صف مستقل لكل يوم (StartDate = اليوم،
/// EndDate = null) — نفس بساطة GenerateWeeklyScheduleCommand، مش Range واحدة لكل الفترة (النمط
/// بيختلف يوم بيوم حسب WeeklyRestDaysMask، فمفيش فايدة من Range موحّدة هنا). Idempotent: أي يوم
/// له صف بالفعل بيتخطّى. بيسجّل/يحدّث EmployeeWeeklyRestDays كمرجع افتراضي للمرة الجاية.
/// </summary>
public sealed record GenerateBulkShiftSchedulesCommand(
    long OrgUnitId, DateOnly FromDate, DateOnly ToDate, long? WorkShiftDefinitionId, int WeeklyRestDaysMask,
    IReadOnlyList<BulkScheduleExceptionInput>? Exceptions) : IRequest<BulkGenerateResultDto>;

public sealed class GenerateBulkShiftSchedulesCommandValidator : AbstractValidator<GenerateBulkShiftSchedulesCommand>
{
    public GenerateBulkShiftSchedulesCommandValidator()
    {
        RuleFor(x => x.OrgUnitId).GreaterThan(0);
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate);
        RuleFor(x => x.WeeklyRestDaysMask).InclusiveBetween(0, 127); // 7 bits — الأحد..السبت
    }
}

public sealed class GenerateBulkShiftSchedulesCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<GenerateBulkShiftSchedulesCommand, BulkGenerateResultDto>
{
    public async Task<BulkGenerateResultDto> Handle(GenerateBulkShiftSchedulesCommand request, CancellationToken cancellationToken)
    {
        var plans = await BulkScheduleResolver.ResolveEmployeePlansAsync(
            db, current.CompanyId, request.OrgUnitId, request.WorkShiftDefinitionId, request.WeeklyRestDaysMask, request.Exceptions, cancellationToken);

        var daysCreated = 0;
        foreach (var plan in plans)
        {
            var employee = await db.Employees.FirstAsync(e => e.Id == plan.EmployeeId, cancellationToken);
            var existing = await ShiftScheduleLookup.FindOverlappingAsync(db, plan.EmployeeId, request.FromDate, request.ToDate, cancellationToken);

            for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
            {
                if (existing.Any(s => s.StartDate <= date && (s.EndDate ?? s.StartDate) >= date))
                {
                    continue; // Idempotent — يوم له جدول بالفعل مايتلمسش.
                }

                var isRestDay = BulkScheduleResolver.IsRestDay(plan.EffectiveWeeklyRestDaysMask, date);
                db.ShiftSchedules.Add(new ShiftSchedule
                {
                    CompanyId = employee.CompanyId,
                    BranchId = employee.BranchId,
                    EmployeeId = plan.EmployeeId,
                    StartDate = date,
                    EndDate = null,
                    WorkShiftDefinitionId = isRestDay ? null : plan.EffectiveWorkShiftDefinitionId,
                    IsRestDay = isRestDay
                });
                daysCreated++;
            }

            var pattern = await db.EmployeeWeeklyRestDays.FirstOrDefaultAsync(w => w.EmployeeId == plan.EmployeeId && w.EffectiveTo == null, cancellationToken);
            if (pattern is null)
            {
                db.EmployeeWeeklyRestDays.Add(new EmployeeWeeklyRestDays
                {
                    CompanyId = employee.CompanyId, EmployeeId = plan.EmployeeId,
                    WeeklyRestDaysMask = plan.EffectiveWeeklyRestDaysMask, EffectiveFrom = request.FromDate, EffectiveTo = null
                });
            }
            else
            {
                pattern.WeeklyRestDaysMask = plan.EffectiveWeeklyRestDaysMask;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new BulkGenerateResultDto { EmployeesProcessed = plans.Count, DaysCreated = daysCreated };
    }
}
