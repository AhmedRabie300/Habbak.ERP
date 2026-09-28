namespace Habbak.ERP.Domain.Common;

/// <summary>My Remarks/Remarks2.md, remark 3.3 — the closed set of screens wired to the generic
/// Attachment mechanism so far. Extend this list (and the matching frontend screen) together
/// whenever another Edit/New screen adds its own "Attachments" action — never accept an arbitrary
/// client-supplied EntityType string beyond this set.</summary>
public static class AttachmentEntityTypes
{
    public const string JournalEntry = "JournalEntry";
    public const string Voucher = "Voucher";
    public const string TreasuryTransfer = "TreasuryTransfer";
    public const string PurchaseInvoice = "PurchaseInvoice";
    public const string PurchaseOrder = "PurchaseOrder";
    public const string GoodsReceipt = "GoodsReceipt";
    public const string SupplierPayment = "SupplierPayment";
    public const string PurchaseReturn = "PurchaseReturn";
    public const string SupplierContract = "SupplierContract";
    public const string RFQ = "RFQ";

    /// <summary>Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.3 — the Documents
    /// tab uploads through this same generic mechanism to get an AttachmentId, which
    /// CreateEmployeeDocumentCommand/UpdateEmployeeDocumentCommand then require before the
    /// EmployeeDocument row itself can be created.</summary>
    public const string EmployeeDocument = "EmployeeDocument";

    /// <summary>Docs/Implementation/Phase-3C-Research.md §3.3 — same reason as EmployeeDocument: the
    /// general upload endpoint needs a known EntityType at upload time even though the real
    /// relationship afterwards is EmploymentContract.AttachmentId, a plain scalar FK.</summary>
    public const string EmploymentContract = "EmploymentContract";

    /// <summary>Docs/Implementation/Phase-3C-Research.md §3.3 — same reason, for EmployeeCertification.AttachmentId.</summary>
    public const string EmployeeCertification = "EmployeeCertification";

    public static readonly string[] All =
    [
        JournalEntry, Voucher, TreasuryTransfer, PurchaseInvoice, PurchaseOrder,
        GoodsReceipt, SupplierPayment, PurchaseReturn, SupplierContract, RFQ, EmployeeDocument,
        EmploymentContract, EmployeeCertification
    ];
}
