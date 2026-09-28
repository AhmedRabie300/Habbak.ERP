namespace Habbak.ERP.Application.Approvals;

/// <summary>
/// Maps an <c>ApprovalWorkflowTrigger.EntityType</c> (a bare logical name like "JournalEntry",
/// the pre-existing IApprovalWorkflowService contract's own vocabulary) to the <see
/// cref="Habbak.ERP.Domain.Common.Screen"/> code an ApprovalWorkflowAssignment is actually made
/// against. Same shape as ScreenCodeCatalog/PostingScreenCatalog — a small static table instead of
/// forcing every caller of IPostingService to already speak in Screen codes. Extend this the same
/// additive way whenever another module starts calling TryStartApprovalAsync directly with its own
/// EntityType (most future callers — Phase 5's advances/penalties/bonuses — will pass their own
/// entity's Screen code as EntityType directly and need no entry here at all).
/// </summary>
public static class ApprovalTriggerScreenMap
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["JournalEntry"] = "ACCOUNTING_JOURNAL_ENTRIES"
    };

    public static string? ScreenCodeFor(string entityType) => Map.GetValueOrDefault(entityType, entityType);
}
