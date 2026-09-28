using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Notifications;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 2.5 — minimal in-app notification (no Email/SMS/
/// Push; those are Phase 6's job, built on top of this same table rather than a new one). Never
/// carries PII: TitleAr/TitleEn/BodyAr/BodyEn stay generic, RelatedEntityType/RelatedEntityId is a
/// polymorphic pointer (same pattern as ApprovalInstance.EntityType) for the frontend's "action"
/// link to the actual record.
/// </summary>
public class Notification : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long RecipientUserId { get; set; }
    public NotificationType Type { get; set; }

    public string TitleAr { get; set; } = null!;
    public string TitleEn { get; set; } = null!;
    public string BodyAr { get; set; } = null!;
    public string BodyEn { get; set; } = null!;

    /// <summary>
    /// Whether this notification needs the recipient to actually do something (an approval
    /// waiting on them) versus purely informational (their own request got approved). Drives the
    /// bell's badge count — Docs/Implementation/Phase-2.5-Research.md: the badge counts this, not
    /// unread count, on purpose.
    /// </summary>
    public bool RequiresAction { get; set; }

    public string? RelatedEntityType { get; set; }
    public long? RelatedEntityId { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

public enum NotificationType
{
    ApprovalPending = 1,
    ApprovalApproved = 2,
    ApprovalRejected = 3,
    ApprovalReassigned = 4,

    /// <summary>Schema-ready for Phase 5 — not sent by anything yet.</summary>
    CustodySettlementRequired = 5,

    /// <summary>Schema-ready for Phase 4 — not sent by anything yet.</summary>
    PayrollExceptionReview = 6,

    /// <summary>Schema-ready for a future document-expiry job — not sent by anything yet.</summary>
    DocumentExpiringSoon = 7,

    /// <summary>Schema-ready for Phase 1.1's termination flow — not sent by anything yet.</summary>
    EmployeeTerminated = 8,

    /// <summary>Schema-ready for Phase 4 — not sent by anything yet.</summary>
    PayrollRunReadyForApproval = 9,

    GeneralInfo = 10
}
