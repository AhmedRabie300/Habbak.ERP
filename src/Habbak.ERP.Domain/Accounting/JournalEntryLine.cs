using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// سطر القيد. Spreading one expense across multiple cost centers (or any other dimension
/// value) is done by adding multiple lines for the same account — there is no separate
/// allocation/percentage table (rule 27).
/// (01-Module-Accounting.md, section 2.2).
/// </summary>
public class JournalEntryLine : AuditableEntity
{
    public long JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = default!;

    public int LineNumber { get; set; }

    /// <summary>Must reference an Account with IsPostable = true.</summary>
    public long AccountId { get; set; }
    public Account Account { get; set; } = default!;

    /// <summary>Exactly one of Debit/Credit is greater than zero.</summary>
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }

    public string CurrencyCode { get; set; } = default!;
    public decimal ExchangeRate { get; set; }

    public decimal BaseCurrencyDebitAmount { get; set; }
    public decimal BaseCurrencyCreditAmount { get; set; }

    public string? Description { get; set; }

    public ICollection<JournalEntryLineDimensionValue> DimensionValues { get; set; } = new List<JournalEntryLineDimensionValue>();
}
