using FluentValidation;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;

namespace Habbak.ERP.Application.Attendance.ShiftSchedules.Queries;

public sealed class BulkSchedulePreviewLineDto
{
    public required long EmployeeId { get; init; }
    public required string EmployeeCode { get; init; }
    public required string EmployeeNameAr { get; init; }
    public required int WorkingDays { get; init; }
    public required int RestDays { get; init; }
    public required int AlreadyScheduledDays { get; init; }
}

/// <summary>Preview بدون أي كتابة — نفس منطق GenerateBulkShiftSchedulesCommand بالظبط
/// (BulkScheduleResolver مشترك) عشان المعاينة تطابق التوليد الفعلي بعدها.</summary>
public sealed record PreviewBulkShiftSchedulesQuery(
    long OrgUnitId, DateOnly FromDate, DateOnly ToDate, long? WorkShiftDefinitionId, int WeeklyRestDaysMask,
    IReadOnlyList<BulkScheduleExceptionInput>? Exceptions) : IRequest<IReadOnlyList<BulkSchedulePreviewLineDto>>;

public sealed class PreviewBulkShiftSchedulesQueryValidator : AbstractValidator<PreviewBulkShiftSchedulesQuery>
{
    public PreviewBulkShiftSchedulesQueryValidator()
    {
        RuleFor(x => x.OrgUnitId).GreaterThan(0);
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate);
    }
}

public sealed class PreviewBulkShiftSchedulesQueryHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<PreviewBulkShiftSchedulesQuery, IReadOnlyList<BulkSchedulePreviewLineDto>>
{
    public async Task<IReadOnlyList<BulkSchedulePreviewLineDto>> Handle(PreviewBulkShiftSchedulesQuery request, CancellationToken cancellationToken)
    {
        var plans = await BulkScheduleResolver.ResolveEmployeePlansAsync(
            db, current.CompanyId, request.OrgUnitId, request.WorkShiftDefinitionId, request.WeeklyRestDaysMask, request.Exceptions, cancellationToken);

        var result = new List<BulkSchedulePreviewLineDto>();
        foreach (var plan in plans)
        {
            var existing = await ShiftScheduleLookup.FindOverlappingAsync(db, plan.EmployeeId, request.FromDate, request.ToDate, cancellationToken);

            int working = 0, rest = 0, already = 0;
            for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
            {
                if (existing.Any(s => s.StartDate <= date && (s.EndDate ?? s.StartDate) >= date))
                {
                    already++;
                    continue;
                }

                if (BulkScheduleResolver.IsRestDay(plan.EffectiveWeeklyRestDaysMask, date)) rest++; else working++;
            }

            result.Add(new BulkSchedulePreviewLineDto
            {
                EmployeeId = plan.EmployeeId, EmployeeCode = plan.EmployeeCode, EmployeeNameAr = plan.EmployeeNameAr,
                WorkingDays = working, RestDays = rest, AlreadyScheduledDays = already
            });
        }

        return result;
    }
}
