namespace Habbak.ERP.Domain.Common;

/// <summary>
/// Docs/Modules/00-Project-Overview.md §12.2 — unified registry of every screen in the system,
/// meant to be shared by the posting engine (PostingTemplate.ScreenId) and the approval engine
/// (ApprovalWorkflowAssignment.ScreenId) instead of each one matching a ScreenCode string on its
/// own (Docs/Implementation/Phase-2-Research.md §1.4). Deliberately does NOT replace
/// ScreenPermission.ScreenCode, the [Screen] attribute, or MenuItem.Code — those stay string-based;
/// unifying them too is a separate, larger decision (Phase-2-Research.md §3.3). System-wide, not
/// company-scoped, same as MenuItem. Seeded additively by ScreenSeedData, same pattern as
/// MenuItemSeedData — not every screen in the system has a row yet, only the ones a posting
/// template or approval workflow can actually be attached to so far; more are added the same way
/// whenever a module needs one.
/// </summary>
public class Screen : AuditableEntity
{
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public string ModuleCode { get; set; } = null!;
}
