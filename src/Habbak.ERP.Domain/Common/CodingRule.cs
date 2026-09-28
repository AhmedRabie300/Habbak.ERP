namespace Habbak.ERP.Domain.Common;

public enum CodeFormat
{
    NumbersOnly = 1,
    LettersOnly = 2,
    LettersAndNumbers = 3
}

/// <summary>
/// Per-(company, screen) code-generation configuration — every "create new record" screen in the
/// system resolves its Code/Number field through this mechanism (manual vs automatic numbering,
/// user-defined starting letters, and a configurable sequence length). The absence of a row for a
/// given screen means "use that screen's built-in default" (see ScreenCodeCatalog) — existing
/// screens keep behaving exactly as before until an admin explicitly reconfigures them here.
/// </summary>
public class CodingRule : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public string ScreenCode { get; set; } = null!;
    public bool IsAutomatic { get; set; }
    public CodeFormat Format { get; set; } = CodeFormat.LettersAndNumbers;

    /// <summary>The letters the code starts with — used by LettersOnly and LettersAndNumbers.</summary>
    public string? Prefix { get; set; }

    /// <summary>Zero-padding width of the numeric part — used by NumbersOnly and LettersAndNumbers.</summary>
    public int SequenceLength { get; set; } = 5;

    /// <summary>The last sequence value handed out — incremented atomically with the entity it numbers
    /// (same DbContext SaveChanges call), same "KNOWN LIMITATION" race-window caveat as the
    /// pre-existing JournalEntryNumberGenerator/VoucherNumberGenerator this replaces.</summary>
    public long LastSequence { get; set; }

    /// <summary>My Remarks/Remarks2.md, remark 4.1 — this row is now "إعدادات الشاشات" (Screen
    /// Settings) more broadly, not just numbering: whether the screen's Attachments panel must
    /// have at least one file before its record can be finalized (checked only on screens that
    /// actually wire AttachmentPanel — see AttachmentEntityTypes).</summary>
    public bool IsAttachmentMandatory { get; set; }

    /// <summary>Whether the screen's "البيان" (notes/description) field must be filled before save.</summary>
    public bool IsDescriptionMandatory { get; set; }
}
