using System.Globalization;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Shared.Enums;

namespace Habbak.ERP.Application.Posting;

/// <summary>
/// Everything a template's lines can read about the document being posted. The source module fills
/// it — and does any aggregating first, a whole shift's invoices into one context for instance
/// (spec rule 13): the engine evaluates what it is given and never gathers data itself.
///
/// Scalar fields plus a flat line list rather than the spec's `Dictionary&lt;string, object&gt;` of
/// anything: every formula in section 4.1 needs either a named figure or the per-line
/// quantity/price/cost, and a typed line list means a misspelled property cannot silently read as zero.
///
/// Field names are case-insensitive; the template editor lists them, but a human still types them.
/// </summary>
public sealed class PostingContext
{
    private readonly IReadOnlyDictionary<string, object?> _fields;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<PostingGroupItem>> _groups;

    private PostingContext(
        IReadOnlyDictionary<string, object?> fields,
        IReadOnlyList<PostingContextLine> lines,
        IReadOnlyDictionary<string, IReadOnlyList<PostingGroupItem>> groups)
    {
        _fields = fields;
        _groups = groups;
        Lines = lines;
    }

    public IReadOnlyList<PostingContextLine> Lines { get; }

    public IEnumerable<string> FieldNames => _fields.Keys;

    public static PostingContext Create(
        IDictionary<string, object?> fields,
        IEnumerable<PostingContextLine>? lines = null,
        IDictionary<string, IReadOnlyList<PostingGroupItem>>? groups = null) =>
        new(
            new Dictionary<string, object?>(fields, StringComparer.OrdinalIgnoreCase),
            (lines ?? []).ToList(),
            new Dictionary<string, IReadOnlyList<PostingGroupItem>>(
                groups ?? new Dictionary<string, IReadOnlyList<PostingGroupItem>>(), StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// The value a FieldCondition trigger compares against, as text — "Cash" for an enum, "42" for a
    /// number. A field the document does not supply reads as no match.
    /// </summary>
    public string? GetText(string name) =>
        _fields.TryGetValue(name, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;

    /// <summary>
    /// A breakdown the document supplies by account — takings per treasury, expenses per account.
    /// An empty group is normal (a shift with no card payments); a group the document never supplies
    /// is a template naming the wrong group, and fails.
    /// </summary>
    public IReadOnlyList<PostingGroupItem> GetGroup(string name) =>
        _groups.TryGetValue(name, out var items)
            ? items
            : throw new BusinessRuleException("POST-GROUP-MISSING", $"المستند مش بيوفّر المجموعة '{name}' اللي القالب محتاجها.");

    public bool HasField(string name) => _fields.ContainsKey(name);

    public object? GetValue(string name) => _fields.TryGetValue(name, out var value) ? value : null;

    /// <summary>A number the template relies on. Missing, null or non-numeric is a hard failure.</summary>
    public decimal GetDecimal(string name)
    {
        if (!_fields.TryGetValue(name, out var value))
        {
            throw PostingErrors.MissingField(name);
        }

        return ToDecimal(name, value) ?? throw PostingErrors.NullField(name);
    }

    /// <summary>An id the template relies on, such as SupplierId. Null is returned as null.</summary>
    public long? GetLong(string name)
    {
        if (!_fields.TryGetValue(name, out var value))
        {
            throw PostingErrors.MissingField(name);
        }

        var number = ToDecimal(name, value);
        return number is null ? null : (long)number.Value;
    }

    internal static decimal? ToDecimal(string name, object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return value switch
            {
                decimal d => d,
                string s => decimal.Parse(s, CultureInfo.InvariantCulture),
                IConvertible c => c.ToDecimal(CultureInfo.InvariantCulture),
                _ => throw new FormatException()
            };
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
        {
            throw new BusinessRuleException("POST-FIELD-NOT-NUMERIC", $"الحقل '{name}' قيمته مش رقم.");
        }
    }
}

public sealed record PostingContextLine(long ItemId, decimal Quantity, decimal UnitPrice, decimal UnitCost);

/// <summary>
/// One entry of a group: an amount and the account it belongs on. <paramref name="Dimensions"/>
/// (dimension id → value id) is for a group whose items belong to different cost centers — a month's
/// depreciation, asset by asset — and wins over the template line's own for the same dimension.
/// </summary>
public sealed record PostingGroupItem(long AccountId, decimal Amount, IReadOnlyDictionary<long, long>? Dimensions = null);

public sealed class TemplatePostingRequest
{
    public required long CompanyId { get; init; }
    public long? BranchId { get; init; }

    /// <summary>Every active template of this screen whose trigger the context admits posts an entry.</summary>
    public required string ScreenCode { get; init; }

    public required SourceModule SourceModule { get; init; }
    public SourceDocumentType? SourceDocumentType { get; init; }
    public required long SourceDocumentId { get; init; }

    public required DateOnly EntryDate { get; init; }
    public required string Description { get; init; }

    /// <summary>Null means the company's base currency, at a rate of 1.</summary>
    public string? CurrencyCode { get; init; }
    public decimal ExchangeRate { get; init; } = 1m;

    public required PostingContext Context { get; init; }

    /// <summary>
    /// Cost centers (dimension id → value id) the source module requires on every line — the fixed
    /// assets' asset-or-branch cost center (08-Module-Maintenance-FixedAssets, rule 27). Applied to each
    /// line for any dimension its template line does not set itself.
    /// </summary>
    public IReadOnlyDictionary<long, long>? Dimensions { get; init; }

    /// <summary>
    /// Mandatory (spec rule 15). Callers should derive it from what the entry represents — the
    /// invoice, the shift, the branch-and-day — so a retry arrives with the same key and gets back
    /// the entry already posted rather than a second one.
    /// </summary>
    public required Guid IdempotencyKey { get; init; }
}

public sealed class TemplatePostingResult
{
    /// <summary>One per template that ran, in ExecutionOrder. Empty only when the screen's templates are all stock-triggered and nothing moved.</summary>
    public required IReadOnlyList<PostedTemplateEntry> Entries { get; init; }

    public required Guid PostingGroupId { get; init; }

    /// <summary>True when the idempotency key had already been used and the existing entries were returned.</summary>
    public required bool IsReplay { get; init; }

    /// <summary>The first entry — the one a document links to; the rest share its PostingGroupId.</summary>
    public JournalEntry? JournalEntry => Entries.Count == 0 ? null : Entries[0].JournalEntry;
}

public sealed record PostedTemplateEntry(JournalEntry JournalEntry, long PostingTemplateId, int TemplateVersionNumber);

internal static class PostingErrors
{
    public static BusinessRuleException MissingField(string name) =>
        new("POST-FIELD-MISSING", $"المستند مش بيوفّر الحقل '{name}' اللي القالب محتاجه.");

    public static BusinessRuleException NullField(string name) =>
        new("POST-FIELD-NULL", $"الحقل '{name}' فاضي — القالب محتاج قيمته.");
}
