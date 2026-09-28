using Habbak.ERP.Application.Attendance.LeaveBalances;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.HR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.LeaveRequests;

/// <summary>
/// نفس منطق "اعتماد نهائي"/"رفض" مُستخدَم في مكانين: SubmitLeaveRequestCommandHandler (لو مفيش
/// Workflow نشطة على الشاشة، اعتماد مباشر زي أي شاشة تانية بدون سلسلة — قاعدة 4) وLeaveRequestApprovalOutcomeHandler
/// (لما محرك الاعتمادات فعليًا يوافق/يرفض). لا SaveChangesAsync هنا — المسؤولية على الـ Caller.
/// </summary>
internal static class LeaveRequestApplyHelpers
{
    public static async Task ApplyApprovedAsync(IApplicationDbContext db, long leaveRequestId, CancellationToken cancellationToken)
    {
        var request = await db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == leaveRequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(LeaveRequest), leaveRequestId);

        var balance = await LeaveBalanceHelpers.FindOrCreateAsync(db, request.EmployeeId, request.LeaveTypeId, request.StartDate.Year, request.CompanyId, cancellationToken);

        balance.Pending -= request.Days;
        balance.Used += request.Days;

        db.LeaveBalanceHistories.Add(new LeaveBalanceHistory
        {
            CompanyId = request.CompanyId,
            LeaveBalance = balance,
            MovementType = LeaveBalanceMovementType.Usage,
            Days = request.Days,
            EffectiveDate = request.StartDate,
            SourceType = "LeaveRequest",
            SourceId = request.Id
        });

        request.Status = HrRequestStatus.Approved;
    }

    public static async Task ApplyRejectedAsync(IApplicationDbContext db, long leaveRequestId, CancellationToken cancellationToken)
    {
        var request = await db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == leaveRequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(LeaveRequest), leaveRequestId);

        var balance = await db.LeaveBalances.FirstOrDefaultAsync(
            b => b.EmployeeId == request.EmployeeId && b.LeaveTypeId == request.LeaveTypeId && b.Year == request.StartDate.Year, cancellationToken);

        // الحجز (Pending) بدون History (قاعدة 26 — Phase-3-Research.md §3.6)، فبيتفك مباشرة بلا صف جديد.
        if (balance is not null)
        {
            balance.Pending -= request.Days;
        }

        request.Status = HrRequestStatus.Rejected;
    }
}
