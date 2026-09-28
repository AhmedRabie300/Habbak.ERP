using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Organization;

/// <summary>System-wide reference catalog — a bank is the same institution regardless of which company references it (Docs/Implementation/HR-Core-Plan.md §1.1).</summary>
public class Bank : AuditableEntity, ILookupEntity
{
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public string? SwiftCode { get; set; }
    public string? Address { get; set; }
    public long? CountryId { get; set; }
    public Country? Country { get; set; }
}
