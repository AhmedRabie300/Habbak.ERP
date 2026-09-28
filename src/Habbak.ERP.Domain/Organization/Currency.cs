using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Organization;

/// <summary>
/// عملة — system-wide reference catalog, shared across every company (like MenuItem/FieldLabel):
/// the currency list itself (EGP, USD, SAR...) is universal, even though each Company picks its
/// own BaseCurrencyId from it (00-Project-Overview.md, section 29 — "عملة مستقلة لكل شركة +
/// عملة محلية موحّدة"). Dedicated table (00-System-Wide-Corrections-02.md, section 2.2).
/// </summary>
public class Currency : AuditableEntity, ILookupEntity
{
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The currency every new record starts with (documents, suppliers, accounts, companies) —
    /// exactly one at a time (Remarks3, item 1). System-wide like the catalog itself.
    /// </summary>
    public bool IsDefault { get; set; }
}
