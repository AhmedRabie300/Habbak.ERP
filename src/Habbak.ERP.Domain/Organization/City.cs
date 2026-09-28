using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Organization;

/// <summary>System-wide reference catalog — cities belong to a country, not to one company (Docs/Implementation/HR-Core-Plan.md §1.1).</summary>
public class City : AuditableEntity, ILookupEntity
{
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public long CountryId { get; set; }
    public Country Country { get; set; } = null!;
}
