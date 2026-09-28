using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.HR;
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
/// Phase 4).</summary>
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
