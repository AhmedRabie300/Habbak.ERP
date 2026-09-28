using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Approvals.Outcomes;

/// <summary>
/// The one entity type actually wired to the engine so far (PostingService, only for
/// PostDraftEntryAsync — Docs/Implementation/Phase-2-Research.md §1.2/§3.1). Approved means exactly
/// what an unconditional post means today: Status -> Posted, PostedAtUtc/PostedBy stamped. Rejected
/// uses JournalEntryStatus.Rejected, already in the enum before this phase.
/// </summary>
public sealed class JournalEntryApprovalOutcomeHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext) : IApprovalOutcomeHandler
{
    public string EntityType => "JournalEntry";

    public async Task ApplyApprovedAsync(long entityId, CancellationToken cancellationToken)
    {
        var entry = await db.JournalEntries.FirstOrDefaultAsync(e => e.Id == entityId, cancellationToken)
            ?? throw new NotFoundException(nameof(JournalEntry), entityId);

        entry.Status = JournalEntryStatus.Posted;
        entry.PostedAtUtc = DateTime.UtcNow;
        entry.PostedBy = currentCompanyContext.UserId;
    }

    public async Task ApplyRejectedAsync(long entityId, CancellationToken cancellationToken)
    {
        var entry = await db.JournalEntries.FirstOrDefaultAsync(e => e.Id == entityId, cancellationToken)
            ?? throw new NotFoundException(nameof(JournalEntry), entityId);

        entry.Status = JournalEntryStatus.Rejected;
    }
}
