using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Sales;

/// <summary>نوع الخصم (04-Module-Sales.md, section 2.2).</summary>
public enum DiscountType
{
    Percentage = 1,
    FixedAmount = 2
}

/// <summary>
/// خصم (04-Module-Sales.md, section 2.2) — screen #3. حقول Happy Hour تظهر شرطيًا لو
/// `IsHappyHour = true` (قاعدة 10)، وترتيب التطبيق بين خصومات متعددة يحدده `ApplicationPriority`
/// تصاعديًا (قاعدة 8) مع توقف عند أول خصم `IsStackable = false` (قاعدة 9).
/// </summary>
public class Discount : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }

    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    public DiscountType DiscountType { get; set; }
    public decimal Value { get; set; }

    /// <summary>الأقل رقمًا يتطبق أولًا (قاعدة 8).</summary>
    public int ApplicationPriority { get; set; }

    /// <summary>لو `false`، تطبيقه يوقف أي خصم تاني على نفس الفاتورة (قاعدة 9).</summary>
    public bool IsStackable { get; set; }

    public decimal? MinInvoiceAmount { get; set; }
    public decimal? MinQuantity { get; set; }

    public bool IsHappyHour { get; set; }
    public TimeOnly? HappyHourFromTime { get; set; }
    public TimeOnly? HappyHourToTime { get; set; }

    public DateOnly EffectiveFromDate { get; set; }
    public DateOnly? EffectiveToDate { get; set; }

    public bool IsActive { get; set; } = true;
}
