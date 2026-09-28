using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Sales;

/// <summary>نوع العميل (04-Module-Sales.md, section 2.1).</summary>
public enum CustomerType
{
    Individual = 1,
    Corporate = 2
}

/// <summary>
/// العميل (04-Module-Sales.md, section 2.1) — screen #1. لا يحمل `PublicId`: خلافًا لجدول الحقول في
/// المواصفة الأصلية، الكود الفعلي في المشتريات/المخازن لا يستخدم `PublicId` إلا في 3 كيانات محاسبية
/// لسبب مُوثَّق خاص بها (`Account`, `Voucher`, `JournalEntry`) — `Customer` هنا يتبع نفس نمط
/// `Supplier` (بدون `PublicId`) للاتساق مع بقية النظام.
/// </summary>
public class Customer : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }

    /// <summary>الفرع الأساسي للعميل، لو محدد.</summary>
    public long? BranchId { get; set; }

    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    public CustomerType CustomerType { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    /// <summary>صفر = ممنوع البيع الآجل نهائيًا ما لم يُحدَّد صراحة أعلى من صفر.</summary>
    public decimal CreditLimit { get; set; }

    public int PaymentTermDays { get; set; }

    /// <summary>حساب مدينون مخصص، أو `null` لاستخدام حساب "عملاء" العام الافتراضي.</summary>
    public long? ReceivableAccountId { get; set; }
    public Account? ReceivableAccount { get; set; }

    /// <summary>الشريحة الحالية — تُعاد حسابها تلقائيًا عند وجود حركات ولاء (قاعدة 13)؛ حاليًا يدوي
    /// لحين بناء `SalesInvoice`/POS اللي بيولّد `LoyaltyTransaction` فعليًا.</summary>
    public long? LoyaltyTierId { get; set; }
    public LoyaltyTier? LoyaltyTier { get; set; }

    public decimal LoyaltyPointsBalance { get; set; }

    public bool IsActive { get; set; } = true;
}
