namespace Habbak.ERP.Domain.Organization;

/// <summary>
/// الشركة — the tenant root itself (Table-per-Company, 00-Project-Overview.md, section 29): every
/// other company-scoped entity's CompanyId points here. Not company-scoped itself — a company
/// can't belong to a company.
/// </summary>
public class Company : Common.AuditableEntity
{
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    /// <summary>السجل التجاري.</summary>
    public string? CommercialRegister { get; set; }

    /// <summary>البطاقة الضريبية.</summary>
    public string? TaxCard { get; set; }

    /// <summary>عملة الشركة الأساسية — كل مبلغ بعملة أجنبية في هذه الشركة يُعاد تقييمه لهذه العملة
    /// (00-Project-Overview.md, section 10/29).</summary>
    public long BaseCurrencyId { get; set; }
    public Currency BaseCurrency { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}
