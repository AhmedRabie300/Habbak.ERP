using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Settings;

/// <summary>What a role may do on one screen. <see cref="ScreenCode"/> is a MenuItem code.</summary>
public class ScreenPermission : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public string ScreenCode { get; set; } = null!;
    public bool CanView { get; set; }
    public bool CanAdd { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanPrint { get; set; }
    public bool CanExport { get; set; }

    /// <summary>Separate from <see cref="CanEdit"/>: whoever prepares a document is not automatically the one who approves it.</summary>
    public bool CanApprove { get; set; }
}
