namespace Habbak.ERP.Domain.Common;

/// <summary>
/// Drives the sidebar navigation tree from the database instead of a static frontend array
/// (00-System-Wide-Corrections-01.md, section 3) — editable later from a settings screen without
/// a new release, and localized per the current user's UI language. System-wide, not
/// company-scoped: every company sees the same menu structure in this pass.
/// </summary>
public class MenuItem : AuditableEntity
{
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public long? ParentId { get; set; }
    public MenuItem? Parent { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Nullable in schema even though the spec marks it mandatory: a pure group header (e.g.
    /// "General Accounting") organizes leaf items but navigates nowhere itself. Required for
    /// every leaf (childless) item — enforced by the seed data, not a DB constraint.
    /// </summary>
    public string? RouteKey { get; set; }

    public string? IconKey { get; set; }
    public bool IsActive { get; set; } = true;
}
