using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Settings;

/// <summary>
/// A role's say on one special button of one screen (ButtonPermissionCatalog) — the buttons that
/// are not the screen's plain view/add/edit/delete/print/export/approve. No row: the button follows
/// the screen permission it would otherwise need. A row decides on its own: enabled lets the role
/// press it even without that screen permission (a cashier who may cancel checks without being an
/// approver); disabled takes it away even from a role that has it.
/// </summary>
public class ButtonPermission : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;

    /// <summary>MenuItem code of the screen the button is on.</summary>
    public string ScreenCode { get; set; } = null!;

    public string ButtonCode { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public bool RequiresAuditLog { get; set; } = true;
}
