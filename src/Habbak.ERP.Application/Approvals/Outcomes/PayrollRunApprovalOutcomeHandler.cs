using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Approvals.Outcomes;

/// <summary>Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.4 — EntityType = Screen.Code
/// مباشرة ("PAY_PAYROLL_RUNS"), نفس نمط LeaveRequestApprovalOutcomeHandler/OvertimeRequestApprovalOutcomeHandler
/// (مُكتشَف تلقائيًا Assembly Scan، صفر تسجيل يدوي). Posted/Paid transitions تبقى Sub-Batch 4.5 —
/// هنا بس Approved/Rejected.</summary>
public sealed class PayrollRunApprovalOutcomeHandler(IApplicationDbContext db) : IApprovalOutcomeHandler
{
    public string EntityType => "PAY_PAYROLL_RUNS";

    public async Task ApplyApprovedAsync(long entityId, CancellationToken cancellationToken)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == entityId, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollRun), entityId);
        run.Status = PayrollRunStatus.Approved;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyRejectedAsync(long entityId, CancellationToken cancellationToken)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == entityId, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollRun), entityId);
        run.Status = PayrollRunStatus.Rejected;
        await db.SaveChangesAsync(cancellationToken);
    }
}
