using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Purchasing;

public enum PurchaseExpenseType
{
    Freight = 1,
    Shipping = 2,
    Customs = 3,
    Loading = 4,
    Insurance = 5,
    Other = 6
}

/// <summary>
/// مصروف شراء (03-Module-Purchasing.md, section 4.8) — screen #8, itemizes what makes up an
/// invoice's additional costs (نقل، شحن، جمارك...). Deliberately does NOT feed
/// PurchaseInvoiceLineBuilder's allocation math — PurchaseInvoice.AdditionalCosts +
/// AdditionalCostAllocationMethod (already shipped and tested) stay the single source of truth for
/// how much lands on inventory cost and by which method; these rows are the itemized breakdown of
/// that total for record-keeping and the "مصروفات الشراء" report, entered independently rather than
/// recomputed from here. No JournalEntryId — IPostingService isn't wired into any Purchasing
/// document yet, same deferral as everywhere else in this module. No workflow/Status: always
/// editable, same as ContractItem/SupplierEvaluation.
/// </summary>
public class PurchaseExpense : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long PurchaseInvoiceId { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }

    public PurchaseExpenseType ExpenseType { get; set; }
    public decimal Amount { get; set; }
    public CostAllocationMethod AllocationMethod { get; set; }

    public string? Notes { get; set; }
}
