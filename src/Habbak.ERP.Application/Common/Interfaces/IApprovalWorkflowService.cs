namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the Approval Workflow Engine (00-Project-Overview.md, section 12).
/// The real implementation — workflow/threshold configuration lookup and ApprovalInstance
/// creation — belongs to the Settings module (07-Module-Settings-Permissions.md), not yet
/// built. IPostingService depends only on this narrow contract so it isn't blocked on that
/// module's existence.
/// </summary>
public interface IApprovalWorkflowService
{
    /// <summary>
    /// If a workflow applies to this document (e.g. its amount exceeds a configured threshold,
    /// rule 11), starts a new ApprovalInstance and returns its Id — the caller must leave the
    /// document in Draft until it is approved. Returns null when no workflow applies, meaning
    /// the document can be posted/approved directly (section 12.3, rule 4).
    /// </summary>
    Task<long?> TryStartApprovalAsync(ApprovalWorkflowTrigger trigger, CancellationToken cancellationToken = default);
}

public sealed class ApprovalWorkflowTrigger
{
    public required long CompanyId { get; init; }

    /// <summary>Logical document type the workflow is configured against, e.g. "JournalEntry".</summary>
    public required string EntityType { get; init; }

    public required long EntityId { get; init; }
    public required decimal Amount { get; init; }

    /// <summary>
    /// The user submitting/posting the document — becomes ApprovalInstance.RequestedByUserId, the
    /// value the Self-Approval Guard checks against (00-Project-Overview.md §12.4 rule 7). Added
    /// for the real Approval Workflow Engine (Phase 2); NullApprovalWorkflowService ignores it.
    /// </summary>
    public required long RequestedByUserId { get; init; }
}
