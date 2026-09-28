using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Posting;

/// <summary>
/// A posting template (00-Posting-Engine-Architecture.md, section 3.1): how one kind of source
/// document becomes a journal entry — which accounts, which amounts, under what conditions.
///
/// A screen can carry several templates (design notes 2026-09-18, note 1): a sale's revenue entry
/// and its cost-of-sales entry, say. Each one that its trigger admits produces an entry of its own;
/// they are all posted in the document's single transaction, in ExecutionOrder, and share one
/// PostingGroupId so they can be read — and reversed — together.
///
/// Versioned exactly like Recipe: editing a template that has already produced an entry creates a
/// new version rather than changing the old one, so every historical entry stays explainable by the
/// rules that actually built it. Versions of one template share a FamilyId.
///
/// Deliberately has no PostingMode, unlike the spec's field table. How documents are grouped into
/// one entry (per document / shift / day) is decided by the source module before it calls the engine
/// (spec rule 13), and for POS that choice lives in BranchPOSSettings per branch.
/// </summary>
public class PostingTemplate : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public string ScreenCode { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>The same for every version of one template; a new template gets a new one.</summary>
    public Guid FamilyId { get; set; } = Guid.NewGuid();

    public PostingTriggerType TriggerType { get; set; } = PostingTriggerType.Always;

    /// <summary>For <see cref="PostingTriggerType.FieldCondition"/>: the document field and the value it must hold.</summary>
    public string? TriggerFieldName { get; set; }
    public string? TriggerFieldValue { get; set; }

    /// <summary>Order among the screen's templates; also the order their entries are numbered in.</summary>
    public int ExecutionOrder { get; set; } = 1;

    public int VersionNumber { get; set; } = 1;
    public long? PreviousVersionId { get; set; }
    public bool IsCurrentVersion { get; set; } = true;

    public bool IsActive { get; set; } = true;
    public bool IsSystemTemplate { get; set; }

    public ICollection<PostingTemplateLine> Lines { get; set; } = new List<PostingTemplateLine>();
}

public class PostingTemplateLine : AuditableEntity
{
    public long PostingTemplateId { get; set; }
    public PostingTemplate? PostingTemplate { get; set; }

    public int LineNumber { get; set; }
    public PostingDirection Direction { get; set; }

    public AccountSourceType AccountSourceType { get; set; }
    public long? FixedAccountId { get; set; }
    public Account? FixedAccount { get; set; }

    /// <summary>Context field holding the account id, for <see cref="AccountSourceType.FromDocument"/>.</summary>
    public string? AccountFieldName { get; set; }

    /// <summary>
    /// A <see cref="CompanyAccountRole"/> name for <see cref="AccountSourceType.FromCompany"/>, or a
    /// registered resolver key for <see cref="AccountSourceType.Resolver"/>. Never free text: both
    /// are checked when the template is saved, not when it first tries to post.
    /// </summary>
    public string? AccountResolverKey { get; set; }

    public AmountFormulaType AmountFormulaType { get; set; }

    /// <summary>The single field most formulas read (or the group name, for a group line).</summary>
    public string? AmountFieldName { get; set; }

    /// <summary>The fields of AddFields / SubtractFields / DivideFields, comma separated, in order.</summary>
    public string? AmountFieldNames { get; set; }

    /// <summary>The constant of <see cref="AmountFormulaType.Multiply"/>.</summary>
    public decimal? AmountMultiplier { get; set; }

    /// <summary>The percentage of <see cref="AmountFormulaType.PercentageOf"/> (14 means 14%).</summary>
    public decimal? AmountPercentage { get; set; }

    public ConditionType ConditionType { get; set; } = ConditionType.None;
    public string? ConditionFieldName { get; set; }
    public string? ConditionFieldValue { get; set; }

    public string? LineDescription { get; set; }

    public ICollection<PostingTemplateLineCostCenter> CostCenters { get; set; } = new List<PostingTemplateLineCostCenter>();
}

public class PostingTemplateLineCostCenter : AuditableEntity
{
    public long PostingTemplateLineId { get; set; }
    public PostingTemplateLine? PostingTemplateLine { get; set; }

    public long CostCenterDimensionId { get; set; }
    public CostCenterDimension? CostCenterDimension { get; set; }

    public CostCenterSourceType SourceType { get; set; }
    public long? FixedValueId { get; set; }

    /// <summary>For FromDocument: the document field holding the entity whose cost center value is wanted.</summary>
    public string? ValueFieldName { get; set; }

    public string? ValueResolverKey { get; set; }

    /// <summary>For FromRelatedEntity: the related record (e.g. "Shift") and its field (e.g. "CashierUserId").</summary>
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityField { get; set; }

    /// <summary>For FromContext: a value the source module supplies that is not a field of the document itself.</summary>
    public string? ContextKey { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>
/// The template exactly as it was when it produced an entry (spec section 3.4). Mandatory for every
/// auto-generated entry: templates get edited, and an old entry has to stay explainable by the rules
/// that built it, not the rules as they read today.
///
/// IdempotencyKey is this entry's own, unique (derived from the request's key and the template), so
/// no entry can be written twice. RequestIdempotencyKey is the document's key, shared by every entry
/// of one posting: a repeated request finds them all and gets them back instead of posting again.
/// </summary>
public class JournalEntryTemplateSnapshot : AuditableEntity
{
    public long JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public long PostingTemplateId { get; set; }
    public int TemplateVersionNumber { get; set; }
    public string TemplateSnapshotJson { get; set; } = null!;

    public Guid IdempotencyKey { get; set; }
    public Guid RequestIdempotencyKey { get; set; }
    public Guid PostingGroupId { get; set; }
}

public enum PostingDirection
{
    Debit = 1,
    Credit = 2
}

/// <summary>
/// When a template runs for a document of its screen (design notes, note 1).
///
/// The notes also proposed a "Manual" trigger. It is left out on purpose: nothing in the system would
/// ever set it off, so it would be a switch that does nothing — the dead-setting problem closed
/// earlier (G-6). It can be added together with whatever is meant to trigger it.
/// </summary>
public enum PostingTriggerType
{
    Always = 1,

    /// <summary>Only when the document moved stock at a cost — a cost-of-sales entry, for instance.</summary>
    HasStockMovement = 2,

    /// <summary>Only when a document field holds a given value (a sale's PaymentType = Cash).</summary>
    FieldCondition = 3
}

/// <summary>
/// The spec lists FromCounterparty / FromItem / FromCategory / Dynamic as four separate kinds, but
/// all four dispatch identically — to a registered resolver by key. What differs is the resolver,
/// not the line, so they are one value here and the resolver itself says what it resolves.
/// </summary>
public enum AccountSourceType
{
    Fixed = 1,
    FromDocument = 2,
    FromCompany = 3,
    Resolver = 4,

    /// <summary>
    /// One journal line per item of a group the document supplies, each with its own account — a
    /// POS shift's takings split by the treasury each payment method feeds, for instance. Always
    /// paired with <see cref="AmountFormulaType.GroupItemAmount"/>; the group's name is kept in
    /// AmountFieldName.
    /// </summary>
    FromGroup = 5
}

/// <summary>
/// Fixed, pre-tested formulas only — no free-text expressions (spec section 4). The arithmetic ones
/// (design notes, note 2) each take named fields and at most one constant: no nesting, no brackets,
/// so what a line computes can always be read off the editor in one glance.
/// </summary>
public enum AmountFormulaType
{
    DirectField = 1,
    SumLineQuantityTimesUnitPrice = 2,
    SumLineQuantityTimesUnitCost = 3,
    SubtotalMinusDiscount = 4,
    SumShiftVarianceLiability = 5,

    /// <summary>Each group item's own amount; see <see cref="AccountSourceType.FromGroup"/>.</summary>
    GroupItemAmount = 6,

    /// <summary>AmountFieldName × AmountMultiplier.</summary>
    Multiply = 7,

    /// <summary>The sum of AmountFieldNames (2 to 5 fields).</summary>
    AddFields = 8,

    /// <summary>The first of AmountFieldNames minus the second.</summary>
    SubtractFields = 9,

    /// <summary>The first of AmountFieldNames divided by the second; dividing by zero stops the posting.</summary>
    DivideFields = 10,

    /// <summary>AmountFieldName × AmountPercentage / 100.</summary>
    PercentageOf = 11
}

public enum ConditionType
{
    None = 1,
    FieldEquals = 2,
    FieldGreaterThanZero = 3,
    FieldNotNull = 4
}

/// <summary>
/// Where a line's cost center value comes from. Every source except Fixed names an entity — a branch,
/// a terminal, a cashier — and the value is the one standing for that entity in a dimension linked to
/// its kind (CostCenterDimension.LinkedEntityType), matched by code.
/// </summary>
public enum CostCenterSourceType
{
    Fixed = 1,

    /// <summary>An entity field of the document itself (its BranchId, its WarehouseId).</summary>
    FromDocument = 2,

    /// <summary>A registered resolver (kept for templates built on the branch resolver).</summary>
    Dynamic = 3,

    /// <summary>A field of a record the document points to — the shift's cashier, the terminal's warehouse.</summary>
    FromRelatedEntity = 4,

    /// <summary>A value the source module supplies alongside the document, not a field on it.</summary>
    FromContext = 5
}
