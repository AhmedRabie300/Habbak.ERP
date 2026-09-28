using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// طريقة الدفع — reference list dedicated to this screen (00-System-Wide-Corrections-02.md,
/// section 2.2: every Lookup screen has its own table, never a shared generic one).
/// </summary>
public class PaymentMethod : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
