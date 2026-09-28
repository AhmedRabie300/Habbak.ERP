using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Organization;

/// <summary>
/// System-wide reference catalog — seeded once (مصري، سعودي، إماراتي...), no admin screen in this
/// phase (Docs/Implementation/HR-Core-Plan.md §1.1, Hybrid decision 2026-09-26).
/// </summary>
public class Nationality : AuditableEntity, ILookupEntity
{
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public long? CountryId { get; set; }
    public Country? Country { get; set; }
}
