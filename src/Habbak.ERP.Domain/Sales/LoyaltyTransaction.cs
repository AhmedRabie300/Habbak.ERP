using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Sales;

/// <summary>04-Module-Sales.md, section 2.1 (قواعد 11-13). Earn/Expire/ManualAdjustment مُعرَّفة
/// لاكتمال المخطط فقط — لسه مفيش أي كود بيكسب نقاط فعليًا (لا SalesInvoice ولا POSInvoice، نفس
/// التأجيل الموثَّق في POSInvoice.cs)، فمفيش صف بالنوع ده هيتسجَّل في هذا البناء. Redeem هو النوع
/// الوحيد القابل للوصول حاليًا — عبر استبدال النقاط في نقطة البيع (مراجعة 2026-09-13).</summary>
public enum LoyaltyTransactionType
{
    Earn = 1,
    Redeem = 2,
    Expire = 3,
    ManualAdjustment = 4
}

/// <summary>قاعدة 12: استبدال النقاط ينقص Customer.LoyaltyPointsBalance فورًا ويُسجَّل هنا. Points
/// موجبة لـEarn، سالبة لـRedeem/Expire — القيمة دايمًا الأثر الصافي على الرصيد.</summary>
public class LoyaltyTransaction : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public LoyaltyTransactionType TransactionType { get; set; }
    public decimal Points { get; set; }

    public string SourceDocumentType { get; set; } = null!;
    public long SourceDocumentId { get; set; }

    public DateOnly TransactionDate { get; set; }
}
