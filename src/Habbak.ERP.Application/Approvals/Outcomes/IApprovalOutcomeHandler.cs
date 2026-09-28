namespace Habbak.ERP.Application.Approvals.Outcomes;

/// <summary>
/// §12.4 rule 6 — "the underlying entity's own status enum reflects the approval outcome, in the
/// same transaction." Each entity type that can carry an ApprovalInstanceId registers its own
/// handler (resolved by <see cref="EntityType"/>, matching ApprovalInstance.EntityType); there is
/// no base document type every approvable entity shares, so this is a small registry instead of a
/// generic reflection-based status setter. An entity type with no registered handler still gets a
/// fully resolved ApprovalInstance (Approved/Rejected), it just has nothing further to flip —
/// today, only JournalEntry (via PostingService) actually triggers an instance at all; every other
/// entity's ApprovalInstanceId stays schema-only until its own module starts calling
/// TryStartApprovalAsync (Docs/Implementation/Phase-2-Research.md §1.3).
/// </summary>
public interface IApprovalOutcomeHandler
{
    string EntityType { get; }

    Task ApplyApprovedAsync(long entityId, CancellationToken cancellationToken);

    Task ApplyRejectedAsync(long entityId, CancellationToken cancellationToken);
}
