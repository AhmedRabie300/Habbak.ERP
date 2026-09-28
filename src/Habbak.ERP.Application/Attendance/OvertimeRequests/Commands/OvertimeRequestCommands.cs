using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.OvertimeRequests.Commands;

/// <summary>موافقة مسبقة (قاعدة 17) — قبل ما الإضافي يحصل، مش توثيق بعدي.</summary>
public sealed record CreateOvertimeRequestCommand(long EmployeeId, DateOnly WorkDate, int PlannedMinutes, OvertimeType OvertimeType, string? Reason) : IRequest<long>;

public sealed class CreateOvertimeRequestCommandValidator : AbstractValidator<CreateOvertimeRequestCommand>
{
    public CreateOvertimeRequestCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.PlannedMinutes).GreaterThan(0);
    }
}

public sealed class CreateOvertimeRequestCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateOvertimeRequestCommand, long>
{
    public async Task<long> Handle(CreateOvertimeRequestCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        var entity = new OvertimeRequest
        {
            CompanyId = employee.CompanyId,
            BranchId = employee.BranchId,
            EmployeeId = request.EmployeeId,
            WorkDate = request.WorkDate,
            PlannedMinutes = request.PlannedMinutes,
            OvertimeType = request.OvertimeType,
            Reason = request.Reason,
            Status = HrRequestStatus.Draft
        };

        db.OvertimeRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

/// <summary>خطوة اعتماد واحدة بس في Phase 3 (Phase-3-Research.md §3.7) — EntityType = Screen.Code
/// مباشرة ("HR_OVERTIME"). Amount = PlannedMinutes (يفتح الباب لعتبة MinAmount بالدقايق مستقبلًا،
/// Phase 4). **Sub-Batch 4.4 (قاعدة 20)**: بيحسب <see cref="OvertimeRequest.ExceedsLimit"/> ضد
/// <c>OvertimeLimitRule</c> الساري وقت <see cref="OvertimeRequest.WorkDate"/> — يومي (الطلب ده لوحده)
/// أو شهري (تراكم الطلبات المعتمدة والمعلّقة لنفس الموظف والشهر + الطلب ده). العلامة دي مايوقفش
/// سلسلة الاعتماد العادية (لسه بتعدّي على DirectManager زي ما هي) — بس تشغيل الرواتب (4.4 كمان،
/// `PayrollCalculationService`) بيتجاهل أي طلب معلّم `ExceedsLimit` لحد ما HR يعمل
/// <see cref="ApproveOvertimeOverLimitCommand"/> — ده البديل العملي لـ"خطوة اعتماد إضافية" لأن
/// `ApprovalWorkflowAssignment` دلوقتي مستوى واحد بس لكل شاشة (Phase-4-Research.md §1.8).</summary>
public sealed record SubmitOvertimeRequestCommand(long Id) : IRequest;

public sealed class SubmitOvertimeRequestCommandValidator : AbstractValidator<SubmitOvertimeRequestCommand>
{
    public SubmitOvertimeRequestCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public sealed class SubmitOvertimeRequestCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, IApprovalWorkflowService approvalWorkflowService)
    : IRequestHandler<SubmitOvertimeRequestCommand>
{
    public async Task Handle(SubmitOvertimeRequestCommand request, CancellationToken cancellationToken)
    {
        var overtimeRequest = await db.OvertimeRequests.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(OvertimeRequest), request.Id);

        if (overtimeRequest.Status != HrRequestStatus.Draft)
        {
            throw new BusinessRuleException("HR-OVERTIME-REQUEST-NOT-DRAFT", "الطلب ده مش في حالة مسودة.");
        }

        overtimeRequest.ExceedsLimit = await ExceedsLimitAsync(overtimeRequest, cancellationToken);

        var instanceId = await approvalWorkflowService.TryStartApprovalAsync(new ApprovalWorkflowTrigger
        {
            CompanyId = current.CompanyId,
            EntityType = "HR_OVERTIME",
            EntityId = overtimeRequest.Id,
            Amount = overtimeRequest.PlannedMinutes,
            RequestedByUserId = current.UserId
        }, cancellationToken);

        if (instanceId is not null)
        {
            overtimeRequest.Status = HrRequestStatus.Pending;
            overtimeRequest.ApprovalInstanceId = instanceId;
        }
        else
        {
            overtimeRequest.Status = HrRequestStatus.Approved;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> ExceedsLimitAsync(OvertimeRequest overtimeRequest, CancellationToken cancellationToken)
    {
        var rules = await db.OvertimeLimitRules.AsNoTracking().Where(r => r.CompanyId == overtimeRequest.CompanyId).ToListAsync(cancellationToken);
        var rule = EffectiveDateRules.ActiveAsOf(rules, overtimeRequest.WorkDate);
        if (rule is null)
        {
            return false;
        }

        if (rule.MaxMinutesPerDay is { } dailyMax && overtimeRequest.PlannedMinutes > dailyMax)
        {
            return true;
        }

        if (rule.MaxMinutesPerMonth is { } monthlyMax)
        {
            var monthStart = new DateOnly(overtimeRequest.WorkDate.Year, overtimeRequest.WorkDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var monthTotal = await db.OvertimeRequests.AsNoTracking()
                .Where(o => o.EmployeeId == overtimeRequest.EmployeeId && o.Id != overtimeRequest.Id
                            && o.WorkDate >= monthStart && o.WorkDate <= monthEnd
                            && (o.Status == HrRequestStatus.Approved || o.Status == HrRequestStatus.Pending))
                .SumAsync(o => o.PlannedMinutes, cancellationToken);

            if (monthTotal + overtimeRequest.PlannedMinutes > monthlyMax)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>HR-only manual override for an over-limit request (§2.6/Sub-Batch 4.4 decision) — the
/// practical stand-in for a second approval step the engine can't yet express conditionally. Endpoint
/// wiring (permission-restricted to HR) lands in Sub-Batch 4.6, same layering as every other command
/// in this file.</summary>
public sealed record ApproveOvertimeOverLimitCommand(long Id) : IRequest;

public sealed class ApproveOvertimeOverLimitCommandValidator : AbstractValidator<ApproveOvertimeOverLimitCommand>
{
    public ApproveOvertimeOverLimitCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public sealed class ApproveOvertimeOverLimitCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<ApproveOvertimeOverLimitCommand>
{
    public async Task Handle(ApproveOvertimeOverLimitCommand request, CancellationToken cancellationToken)
    {
        var overtimeRequest = await db.OvertimeRequests.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(OvertimeRequest), request.Id);

        if (!overtimeRequest.ExceedsLimit)
        {
            throw new BusinessRuleException("HR-OVERTIME-NOT-OVER-LIMIT", "الطلب ده مش متجاوز أي حد أصلًا.");
        }

        overtimeRequest.HrOverrideApprovedByUserId = current.UserId;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record CancelOvertimeRequestCommand(long Id) : IRequest;

public sealed class CancelOvertimeRequestCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelOvertimeRequestCommand>
{
    public async Task Handle(CancelOvertimeRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.OvertimeRequests.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(OvertimeRequest), request.Id);

        if (entity.Status is HrRequestStatus.Rejected or HrRequestStatus.Cancelled)
        {
            throw new BusinessRuleException("HR-OVERTIME-REQUEST-ALREADY-CLOSED", "الطلب ده مقفول بالفعل.");
        }

        entity.Status = HrRequestStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
    }
}
