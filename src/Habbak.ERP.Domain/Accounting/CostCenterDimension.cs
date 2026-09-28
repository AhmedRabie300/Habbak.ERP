using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// Reserved Code for the system-seeded "Cost Center" dimension (01-Module-Accounting.md, rule 29).
/// Reports keyed to a cost center resolve this dimension by Code, never by its display Name.
/// </summary>
public static class CostCenterDimensionCodes
{
    public const string CostCenter = "COST_CENTER";
}

/// <summary>
/// Which master-data screen supplies a cost center's selectable values. None means values are
/// entered directly under the cost center itself (CostCenterDimensionValue); any other entry
/// means the cost center's values are that screen's own live, active records instead.
/// </summary>
public enum CostCenterLinkedEntityType
{
    None = 0,
    Branch = 1,

    // Posting engine (design notes 2026-09-18, note 3): a template line can take its cost center from
    // any of these on the document. Values mirror the records by code; see PostingEntityValueMapper.
    POSTerminal = 2,
    Warehouse = 3,

    /// <summary>No cashier master record exists yet: values are entered by hand, coded with the user number.</summary>
    Cashier = 4,
    Customer = 5,
    Supplier = 6
}

/// <summary>
/// مركز تكلفة — a single unified mechanism for every cost-center-style classification on a
/// journal line: branch, sales channel, cashier, etc. (01-Module-Accounting.md, section 2.1).
/// </summary>
public class CostCenterDimension : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public bool IsActive { get; set; } = true;

    /// <summary>None (default): values live in <see cref="Values"/>. Otherwise, values are read
    /// live from that other screen's active records (e.g. Branch — the Branches screen) and
    /// <see cref="Values"/> stays empty/unused.</summary>
    public CostCenterLinkedEntityType LinkedEntityType { get; set; } = CostCenterLinkedEntityType.None;

    public ICollection<CostCenterDimensionValue> Values { get; set; } = new List<CostCenterDimensionValue>();
}
