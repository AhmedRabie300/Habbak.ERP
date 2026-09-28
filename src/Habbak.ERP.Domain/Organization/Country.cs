using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Organization;

/// <summary>
/// System-wide reference catalog (like <see cref="Currency"/>) — the country list is universal,
/// not defined per company (Docs/Implementation/HR-Core-Plan.md §1.1, Batch B1).
/// </summary>
public class Country : AuditableEntity, ILookupEntity
{
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    /// <summary>ISO 3166-1 code (e.g. "EG") — optional, for integrations that need it.</summary>
    public string? IsoCode { get; set; }
}
