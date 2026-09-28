namespace Habbak.ERP.Domain.Common;

/// <summary>
/// Drives DataGrid column headers and input field labels from the database instead of hardcoded
/// i18n keys (00-System-Wide-Corrections-01.md, section 4) — editable later from a settings
/// screen without a new release. Deliberately excludes buttons, validation/error text and Toast
/// messages, which stay in react-i18next (same doc, section 4.2) so the database only carries the
/// text actually expected to be customized per deployment.
/// </summary>
public class FieldLabel : AuditableEntity
{
    /// <summary>References a MenuItem.Code for a top-level screen, or an independent screen code
    /// for a sub-screen not directly listed in the menu (e.g. an Edit screen).</summary>
    public string ScreenCode { get; set; } = null!;

    /// <summary>The field's technical name as it appears in the DTO (e.g. "UnitPrice").</summary>
    public string FieldCode { get; set; } = null!;

    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
}
