using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Shared.Enums;

namespace Habbak.ERP.Domain.Posting;

/// <summary>
/// A document that could not be posted, and why (spec section 10, report 2 "القيود الفاشلة").
///
/// Written outside the document's own transaction — the failure rolls the document back, so a row
/// saved alongside it would vanish with it. That is exactly the case this table exists for: the
/// invoice is still a draft, the cashier saw an error once, and nobody else would ever know.
///
/// Marked resolved, not deleted, once the same document posts: how often a document failed, and
/// with what, is itself worth seeing when a template is being put right.
/// </summary>
public class PostingFailure : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public string ScreenCode { get; set; } = null!;
    public SourceModule SourceModule { get; set; }
    public long SourceDocumentId { get; set; }
    public string Description { get; set; } = null!;

    public string ErrorCode { get; set; } = null!;
    public string ErrorMessage { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; }
    public long? UserId { get; set; }

    public bool IsResolved { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public long? ResolvedByJournalEntryId { get; set; }
    public JournalEntry? ResolvedByJournalEntry { get; set; }
}
