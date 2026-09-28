using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// سند قبض/صرف (01-Module-Accounting.md, section 2.3).
/// </summary>
public class Voucher : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public Guid? PublicId { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public VoucherType VoucherType { get; set; }
    public string VoucherNumber { get; set; } = default!;
    public DateOnly VoucherDate { get; set; }

    /// <summary>البيان — optional free-text narration for the voucher.</summary>
    public string? Description { get; set; }

    /// <summary>The treasury/bank account affected by this voucher.</summary>
    public long TreasuryAccountId { get; set; }
    public Account TreasuryAccount { get; set; } = default!;

    public CounterpartyType CounterpartyType { get; set; }

    /// <summary>Required when CounterpartyType is Customer/Supplier/Employee.</summary>
    public long? CounterpartyId { get; set; }

    /// <summary>
    /// Required only when CounterpartyType = Other — the direct account (general expense,
    /// sundry income...) that forms the other leg of the double entry when there is no
    /// known customer/supplier/employee ledger account to resolve it from (rule 28).
    /// Must reference an Account with IsPostable = true.
    /// </summary>
    public long? DirectAccountId { get; set; }
    public Account? DirectAccount { get; set; }

    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = default!;
    public decimal ExchangeRate { get; set; }
    public decimal BaseCurrencyAmount { get; set; }

    /// <summary>Set when this voucher settles a specific invoice.</summary>
    public long? RelatedInvoiceId { get; set; }

    public VoucherStatus Status { get; set; } = VoucherStatus.Draft;

    /// <summary>Set once posted via IPostingService.</summary>
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}
