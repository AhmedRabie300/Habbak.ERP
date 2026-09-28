using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Purchasing;

/// <summary>
/// المورد (03-Module-Purchasing.md, section 4.1). Deviates from the doc's own field table in one
/// place: the doc lists both `DefaultPayableAccountId` ("company settings' default, overridden by
/// this supplier's own PayableAccountId if set") and `PayableAccountId` on the same entity — the
/// former implies a company-level default-accounts settings screen that doesn't exist anywhere in
/// this codebase yet (Accounting has no such settings entity), so only the real per-supplier
/// override (`PayableAccountId`) is implemented here; a future company-settings screen can add the
/// fallback default without touching this entity.
/// </summary>
public class Supplier : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    public SupplierPaymentTerms PaymentTerms { get; set; }
    public decimal? CreditLimit { get; set; }
    public string CurrencyCode { get; set; } = null!;

    public long? DefaultWarehouseId { get; set; }
    public Warehouse? DefaultWarehouse { get; set; }

    /// <summary>حساب المورد (دائنون) — يتجاوز أي حساب افتراضي عام لو حُدِّد.</summary>
    public long? PayableAccountId { get; set; }
    public Account? PayableAccount { get; set; }

    /// <summary>حساب المصروفات الإضافية (نقل، شحن) المرتبطة بمشتريات هذا المورد.</summary>
    public long? ExpenseAccountId { get; set; }
    public Account? ExpenseAccount { get; set; }

    public bool IsActive { get; set; } = true;
}
