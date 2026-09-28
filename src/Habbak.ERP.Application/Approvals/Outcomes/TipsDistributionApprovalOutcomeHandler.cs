using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Approvals.Outcomes;

/// <summary>Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.6 — EntityType = Screen.Code
/// مباشرة ("PAY_TIPS_DISTRIBUTION"), نفس نمط PayrollRunApprovalOutcomeHandler (مُكتشَف تلقائيًا).</summary>
public sealed class TipsDistributionApprovalOutcomeHandler(IApplicationDbContext db) : IApprovalOutcomeHandler
{
    public string EntityType => "PAY_TIPS_DISTRIBUTION";

    public async Task ApplyApprovedAsync(long entityId, CancellationToken cancellationToken)
    {
        var entity = await db.TipsDistributions.FirstOrDefaultAsync(t => t.Id == entityId, cancellationToken)
            ?? throw new NotFoundException(nameof(TipsDistribution), entityId);
        entity.Status = TipsDistributionStatus.Approved;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyRejectedAsync(long entityId, CancellationToken cancellationToken)
    {
        var entity = await db.TipsDistributions.FirstOrDefaultAsync(t => t.Id == entityId, cancellationToken)
            ?? throw new NotFoundException(nameof(TipsDistribution), entityId);
        entity.Status = TipsDistributionStatus.Rejected;
        await db.SaveChangesAsync(cancellationToken);
    }
}
