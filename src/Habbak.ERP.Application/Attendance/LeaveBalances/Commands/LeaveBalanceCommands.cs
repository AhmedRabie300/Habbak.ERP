using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.LeaveBalances.Commands;

/// <summary>تعديل يدوي بصلاحية وسبب (§5.1 رقم 12) — بيغيّر AccruedThisYear (تصحيح/مكافأة رصيد)،
/// مش Used مباشرة (اللي بيتغيّر بس عن طريق اعتماد LeaveRequest). صف LeaveBalanceHistory إلزامي
/// (MovementType = Adjustment، قاعدة 26).</summary>
public sealed record AdjustLeaveBalanceCommand(long EmployeeId, long LeaveTypeId, int Year, decimal Days, string Reason) : IRequest<long>;

public sealed class AdjustLeaveBalanceCommandValidator : AbstractValidator<AdjustLeaveBalanceCommand>
{
    public AdjustLeaveBalanceCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.LeaveTypeId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("سبب التعديل إلزامي.");
    }
}

public sealed class AdjustLeaveBalanceCommandHandler(IApplicationDbContext db) : IRequestHandler<AdjustLeaveBalanceCommand, long>
{
    public async Task<long> Handle(AdjustLeaveBalanceCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        if (!await db.LeaveTypes.AnyAsync(l => l.Id == request.LeaveTypeId, cancellationToken))
        {
            throw new NotFoundException(nameof(LeaveType), request.LeaveTypeId);
        }

        var balance = await LeaveBalanceHelpers.FindOrCreateAsync(db, request.EmployeeId, request.LeaveTypeId, request.Year, employee.CompanyId, cancellationToken);
        balance.AccruedThisYear += request.Days;

        db.LeaveBalanceHistories.Add(new LeaveBalanceHistory
        {
            CompanyId = employee.CompanyId,
            LeaveBalance = balance,
            MovementType = LeaveBalanceMovementType.Adjustment,
            Days = request.Days,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SourceType = "ManualAdjustment",
            Reason = request.Reason
        });

        await db.SaveChangesAsync(cancellationToken);
        return balance.Id;
    }
}

/// <summary>Idempotent لكل موظف وشهر (مفتاح: LeaveBalanceHistory.SourceType = "MonthlyAccrual" +
/// SourceId = Year*100+Month، لكل LeaveBalance) — قاعدة 23. AnnualDays ÷ 12 (الـFallback؛
/// LeaveEntitlementRule الفعلي في Phase 4). Job بيتنفَّذ عن طريق Endpoint صريح (نفس نمط
/// DepreciationRun) — مفيش Scheduler حقيقي في هذه المرحلة.</summary>
public sealed record AccrueMonthlyLeaveCommand(int Year, int Month) : IRequest<int>;

public sealed class AccrueMonthlyLeaveCommandValidator : AbstractValidator<AccrueMonthlyLeaveCommand>
{
    public AccrueMonthlyLeaveCommandValidator()
    {
        RuleFor(x => x.Year).GreaterThan(2000);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class AccrueMonthlyLeaveCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current) : IRequestHandler<AccrueMonthlyLeaveCommand, int>
{
    public async Task<int> Handle(AccrueMonthlyLeaveCommand request, CancellationToken cancellationToken)
    {
        var idempotencyKey = request.Year * 100 + request.Month;

        var leaveTypes = await db.LeaveTypes
            .Where(l => l.CompanyId == current.CompanyId && l.IsActive && l.AccrualMethod == LeaveAccrualMethod.Monthly)
            .ToListAsync(cancellationToken);
        if (leaveTypes.Count == 0)
        {
            return 0;
        }

        var employees = await db.Employees
            .Where(e => e.CompanyId == current.CompanyId && e.Status == EmployeeStatus.Active)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        var created = 0;
        foreach (var employeeId in employees)
        {
            foreach (var leaveType in leaveTypes)
            {
                var balance = await LeaveBalanceHelpers.FindOrCreateAsync(db, employeeId, leaveType.Id, request.Year, current.CompanyId, cancellationToken);

                var alreadyAccrued = balance.Id != 0 && await db.LeaveBalanceHistories.AnyAsync(
                    h => h.LeaveBalanceId == balance.Id && h.SourceType == "MonthlyAccrual" && h.SourceId == idempotencyKey, cancellationToken);
                if (alreadyAccrued)
                {
                    continue;
                }

                var monthlyDays = Math.Round(leaveType.AnnualDays / 12m, 2);
                balance.AccruedThisYear += monthlyDays;

                db.LeaveBalanceHistories.Add(new LeaveBalanceHistory
                {
                    CompanyId = current.CompanyId,
                    LeaveBalance = balance,
                    MovementType = LeaveBalanceMovementType.Accrual,
                    Days = monthlyDays,
                    EffectiveDate = new DateOnly(request.Year, request.Month, 1),
                    SourceType = "MonthlyAccrual",
                    SourceId = idempotencyKey,
                    Reason = $"استحقاق شهري {request.Month}/{request.Year}"
                });
                created++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return created;
    }
}
