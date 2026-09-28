using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Purchasing;

/// <summary>
/// توزيع سداد المورد على فواتيره (Remarks4, item 7). سداد المورد نفسه سند صرف
/// (<see cref="Voucher"/> بنوع Payment ومورد كطرف) — الجديد هنا إن الدفعة الواحدة تتوزّع على أكتر
/// من فاتورة بدل ما تتربط بفاتورة واحدة عن طريق Voucher.RelatedInvoiceId.
///
/// الصفوف دي بتتكتب والسند لسه مسودة، وبتتطبّق على PurchaseInvoice.AmountPaid عند الترحيل
/// وبترجع عند الإلغاء — فمجموعها لازم يساوي قيمة السند، وكل سطر مايزيدش عن المتبقي على فاتورته.
/// </summary>
public class SupplierPaymentAllocation : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long VoucherId { get; set; }
    public Voucher? Voucher { get; set; }

    public long PurchaseInvoiceId { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }

    /// <summary>بعملة السند — نفس عملة الفاتورة (التوزيع بيرفض اختلاف العملة).</summary>
    public decimal Amount { get; set; }
}
