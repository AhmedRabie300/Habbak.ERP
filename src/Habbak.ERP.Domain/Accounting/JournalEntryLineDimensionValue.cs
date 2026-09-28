using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// The value chosen for one dimension on one journal entry line. CostCenterDimensionValueId
/// must belong to the same CostCenterDimensionId set on this row — mixing values from a
/// different dimension is rejected (rule 21).
/// (01-Module-Accounting.md, section 2.2).
/// </summary>
public class JournalEntryLineDimensionValue : AuditableEntity
{
    public long JournalEntryLineId { get; set; }
    public JournalEntryLine JournalEntryLine { get; set; } = default!;

    public long CostCenterDimensionId { get; set; }
    public CostCenterDimension CostCenterDimension { get; set; } = default!;

    public long CostCenterDimensionValueId { get; set; }
    public CostCenterDimensionValue CostCenterDimensionValue { get; set; } = default!;
}
