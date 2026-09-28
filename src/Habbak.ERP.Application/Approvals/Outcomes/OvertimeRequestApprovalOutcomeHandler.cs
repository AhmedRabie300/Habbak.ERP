using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.HR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Approvals.Outcomes;

/// <summary>EntityType = Screen.Code مباشرة ("HR_OVERTIME"). مفيش رصيد يتفك زي LeaveRequest —
/// الإضافي مالوش Pending Hold، بس تغيير حالة.</summary>
public sealed class OvertimeRequestApprovalOutcomeHandler(IApplicationDbContext db) : IApprovalOutcomeHandler
{
    public string EntityType => "HR_OVERTIME";

    public async Task ApplyApprovedAsync(long entityId, CancellationToken cancellationToken)
    {
        var entity = await db.OvertimeRequests.FirstOrDefaultAsync(o => o.Id == entityId, cancellationToken)
            ?? throw new NotFoundException(nameof(OvertimeRequest), entityId);

        entity.Status = HrRequestStatus.Approved;
    }

    public async Task ApplyRejectedAsync(long entityId, CancellationToken cancellationToken)
    {
        var entity = await db.OvertimeRequests.FirstOrDefaultAsync(o => o.Id == entityId, cancellationToken)
            ?? throw new NotFoundException(nameof(OvertimeRequest), entityId);

        entity.Status = HrRequestStatus.Rejected;
    }
}
