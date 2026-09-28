namespace Habbak.ERP.Domain.Accounting;

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

public enum AccountNature
{
    Debit = 1,
    Credit = 2
}

public enum JournalEntryStatus
{
    Draft = 1,
    Posted = 2,
    Rejected = 3,
    Reversed = 4
}

/// <summary>
/// Fixed values for JournalEntry.SourceDocumentType (01-Module-Accounting.md, section 2.2).
/// </summary>
public enum SourceDocumentType
{
    Invoice = 1,
    PaymentVoucher = 2,
    ReceiptVoucher = 3,
    ProductionOrder = 4,
    Payroll = 5,
    Depreciation = 6,
    InventoryAdjustment = 7,
    TreasuryTransfer = 8,

    /// <summary>My Remarks/Remarks2.md, bugs 1.4/3.9 — the Chart of Accounts opening-balance
    /// screen's own posted journal entries.</summary>
    AccountOpeningBalance = 9,

    // Posting engine documents (Docs/Posting-Engine-Implementation-Plan.md, stage 3).
    Return = 10,
    DeliveryOrder = 11,
    Shift = 12,

    /// <summary>One branch's POS sales for a day, posted by "إقفال اليوم". SourceDocumentId is the branch.</summary>
    POSDay = 13,
    DrawerExpense = 14,
    WarehouseDocument = 15,
    InventoryCount = 16,
    Waste = 17,

    // Fixed assets and maintenance (08-Module-Maintenance-FixedAssets). Depreciation (6) already existed.
    FixedAssetAcquisition = 18,
    AssetDisposal = 19,
    MaintenanceRequest = 20,

    /// <summary>Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.5 — Payroll (5) already
    /// covers the accrual/payment entries; this is specifically for the tips reclassification
    /// (TipsPayable → SalariesPayable) folded into the same accrual entry (§6.2: "داخل قيد الاستحقاق").
    /// EmployeeAdvance/EndOfService stay unadded — both need their own Phase 5 entity first
    /// (Phase-4-Research.md §2.5, HR-MASTER-PLAN.md §Phase 4 scope note on "EOS Settlement").</summary>
    TipsDistribution = 21
}

public enum VoucherType
{
    Receipt = 1,
    Payment = 2
}

public enum CounterpartyType
{
    Customer = 1,
    Supplier = 2,
    Employee = 3,
    Other = 4
}

public enum VoucherStatus
{
    Draft = 1,
    Posted = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum TreasuryTransferStatus
{
    Draft = 1,
    Posted = 2
}

public enum CustodyStatus
{
    Open = 1,
    PartiallySettled = 2,
    Closed = 3,
    Cancelled = 4
}

public enum BankReconciliationStatus
{
    InProgress = 1,
    Completed = 2
}

/// <summary>
/// Fixed values for BankReconciliationLine.SystemTransactionType (01-Module-Accounting.md, rule 23).
/// </summary>
public enum SystemTransactionType
{
    Voucher = 1,
    TreasuryTransfer = 2,
    JournalEntry = 3
}

public enum AccountingPeriodStatus
{
    Open = 1,
    Closed = 2
}

/// <summary>
/// Fixed values for PeriodCloseChecklistItem.ItemKey (01-Module-Accounting.md, section 2.6 and rule 30).
/// </summary>
public enum PeriodCloseChecklistItemKey
{
    AllJournalsPosted = 1,
    AllVouchersPosted = 2,
    AllTransfersPosted = 3,
    LastDayCashReconciled = 4,
    BankReconciled = 5,
    AllETASent = 6
}
