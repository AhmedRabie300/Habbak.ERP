using FluentValidation;
using Habbak.ERP.Application.Attendance.LeaveBalances;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.LeaveRequests.Commands;

/// <summary>قاعدة 22-23 — الرصيد المتاح (Available − Pending) لازم يغطّي الأيام المطلوبة (إلا لأنواع
/// AccrualMethod = None زي المرضية). Days بتتحسب بـ LeaveDaysCalculator (Calendar/WorkingDays حسب
/// HrSettings.LeaveDayCountingMode).</summary>
public sealed record CreateLeaveRequestCommand(long EmployeeId, long LeaveTypeId, DateOnly StartDate, DateOnly EndDate, string? Reason) : IRequest<long>;

public sealed class CreateLeaveRequestCommandValidator : AbstractValidator<CreateLeaveRequestCommand>
{
    public CreateLeaveRequestCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.LeaveTypeId).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}

public sealed class CreateLeaveRequestCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateLeaveRequestCommand, long>
{
    public async Task<long> Handle(CreateLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        var leaveType = await db.LeaveTypes.FirstOrDefaultAsync(l => l.Id == request.LeaveTypeId, cancellationToken)
            ?? throw new NotFoundException(nameof(LeaveType), request.LeaveTypeId);

        if (leaveType.GenderRestriction is not null)
        {
            var gender = await db.EmployeePersonalDataRows.Where(p => p.EmployeeId == request.EmployeeId)
                .Select(p => (Gender?)p.Gender).FirstOrDefaultAsync(cancellationToken);
            if (gender is not null && gender != leaveType.GenderRestriction)
            {
                throw new BusinessRuleException("HR-LEAVE-GENDER-RESTRICTED", "نوع الإجازة ده مقصور على جنس مختلف.");
            }
        }

        var (days, _, _) = await LeaveDaysCalculator.CalculateAsync(db, request.EmployeeId, employee.CompanyId, request.StartDate, request.EndDate, cancellationToken);

        if (leaveType.MaxDaysPerRequest is { } max && days > max)
        {
            throw new BusinessRuleException("HR-LEAVE-EXCEEDS-MAX-PER-REQUEST", $"أقصى عدد أيام مسموح به لهذا النوع في الطلب الواحد هو {max}.");
        }

        var balance = await LeaveBalanceHelpers.FindOrCreateAsync(db, request.EmployeeId, request.LeaveTypeId, request.StartDate.Year, employee.CompanyId, cancellationToken);

        if (leaveType.AccrualMethod != LeaveAccrualMethod.None && days > balance.Available)
        {
            throw new BusinessRuleException("HR-LEAVE-INSUFFICIENT-BALANCE", "الرصيد المتاح أقل من عدد الأيام المطلوبة.");
        }

        balance.Pending += days;

        var entity = new LeaveRequest
        {
            CompanyId = employee.CompanyId,
            BranchId = employee.BranchId,
            EmployeeId = request.EmployeeId,
            LeaveTypeId = request.LeaveTypeId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Days = days,
            Reason = request.Reason,
            Status = HrRequestStatus.Draft
        };

        db.LeaveRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

/// <summary>قاعدة 4 — شاشة من غير Workflow نشطة تفضل تعتمد مباشرة (نفس منطق PostingService/JournalEntry).</summary>
public sealed record SubmitLeaveRequestCommand(long Id) : IRequest;

public sealed class SubmitLeaveRequestCommandValidator : AbstractValidator<SubmitLeaveRequestCommand>
{
    public SubmitLeaveRequestCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public sealed class SubmitLeaveRequestCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, IApprovalWorkflowService approvalWorkflowService)
    : IRequestHandler<SubmitLeaveRequestCommand>
{
    public async Task Handle(SubmitLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(LeaveRequest), request.Id);

        if (leaveRequest.Status != HrRequestStatus.Draft)
        {
            throw new BusinessRuleException("HR-LEAVE-REQUEST-NOT-DRAFT", "الطلب ده مش في حالة مسودة.");
        }

        var instanceId = await approvalWorkflowService.TryStartApprovalAsync(new ApprovalWorkflowTrigger
        {
            CompanyId = current.CompanyId,
            EntityType = "HR_LEAVE_REQUESTS",
            EntityId = leaveRequest.Id,
            Amount = 0,
            RequestedByUserId = current.UserId
        }, cancellationToken);

        if (instanceId is not null)
        {
            leaveRequest.Status = HrRequestStatus.Pending;
            leaveRequest.ApprovalInstanceId = instanceId;
        }
        else
        {
            await LeaveRequestApplyHelpers.ApplyApprovedAsync(db, leaveRequest.Id, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>قاعدة 27 — قبل بداية الإجازة بس؛ بعد ما تبدأ محتاجة مسار اعتماد إلغاء منفصل (Known
/// Limitation، Phase-3-Final.md).</summary>
public sealed record CancelLeaveRequestCommand(long Id) : IRequest;

public sealed class CancelLeaveRequestCommandValidator : AbstractValidator<CancelLeaveRequestCommand>
{
    public CancelLeaveRequestCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public sealed class CancelLeaveRequestCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelLeaveRequestCommand>
{
    public async Task Handle(CancelLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(LeaveRequest), request.Id);

        if (leaveRequest.Status is HrRequestStatus.Rejected or HrRequestStatus.Cancelled)
        {
            throw new BusinessRuleException("HR-LEAVE-REQUEST-ALREADY-CLOSED", "الطلب ده مقفول بالفعل.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today >= leaveRequest.StartDate)
        {
            throw new BusinessRuleException(
                "HR-LEAVE-CANCEL-AFTER-START",
                "لا يمكن إلغاء إجازة بدأت بالفعل من هذه الشاشة مباشرة — يحتاج مسار اعتماد إلغاء منفصل (خارج نطاق Phase 3).");
        }

        var balance = await db.LeaveBalances.FirstOrDefaultAsync(
            b => b.EmployeeId == leaveRequest.EmployeeId && b.LeaveTypeId == leaveRequest.LeaveTypeId && b.Year == leaveRequest.StartDate.Year, cancellationToken);

        if (balance is not null)
        {
            if (leaveRequest.Status == HrRequestStatus.Approved)
            {
                balance.Used -= leaveRequest.Days;
                db.LeaveBalanceHistories.Add(new LeaveBalanceHistory
                {
                    CompanyId = leaveRequest.CompanyId,
                    LeaveBalance = balance,
                    MovementType = LeaveBalanceMovementType.Reversal,
                    Days = leaveRequest.Days,
                    EffectiveDate = today,
                    SourceType = "LeaveRequest",
                    SourceId = leaveRequest.Id,
                    Reason = "إلغاء إجازة معتمدة قبل بدايتها (قاعدة 27)"
                });
            }
            else
            {
                balance.Pending -= leaveRequest.Days;
            }
        }

        leaveRequest.Status = HrRequestStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
    }
}
