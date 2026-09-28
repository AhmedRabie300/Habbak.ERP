using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// دليل الحسابات (01-Module-Accounting.md, section 2.1).
/// </summary>
public class Account : AuditableEntity, ICompanyScopedEntity
{
    public Guid? PublicId { get; set; }

    /// <summary>Null only when <see cref="IsSharedAcrossCompanies"/> is true.</summary>
    public long? CompanyId { get; set; }

    public string Code { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;

    public long? ParentId { get; set; }
    public Account? Parent { get; set; }

    public int Level { get; set; }
    public AccountType AccountType { get; set; }
    public AccountNature Nature { get; set; }

    /// <summary>False for grouping/parent accounts — cannot receive direct journal lines.</summary>
    public bool IsPostable { get; set; }

    /// <summary>Null = company's base currency (00-Project-Overview.md, section 10).</summary>
    public string? CurrencyCode { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The account's definition is shared across all companies, but balances remain fully
    /// separate per company (rule 12) — every JournalEntryLine posted to it still carries its
    /// own CompanyId.
    /// </summary>
    public bool IsSharedAcrossCompanies { get; set; }

    public ICollection<AccountDimensionLink> DimensionLinks { get; set; } = new List<AccountDimensionLink>();
}
