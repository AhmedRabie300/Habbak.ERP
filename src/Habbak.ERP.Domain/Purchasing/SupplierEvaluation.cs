using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Purchasing;

/// <summary>
/// تقييم أداء المورد (03-Module-Purchasing.md, section 8, screen #12: "جودة، التزام بالوقت، التزام
/// بالكمية"). No dedicated entity ever appears in section 4 — this is entirely new, designed from
/// the screens table's own three named criteria. Each score is 0-100; OverallScore is stored (not
/// computed on read) as their simple average, taken at save time. No workflow/Status: same
/// always-editable master-data pattern as Supplier itself — an evaluation is a historical record,
/// corrected in place rather than reversed by a counter-document.
/// </summary>
public class SupplierEvaluation : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public DateOnly EvaluationDate { get; set; }

    public decimal QualityScore { get; set; }
    public decimal DeliveryTimeScore { get; set; }
    public decimal QuantityComplianceScore { get; set; }

    /// <summary>Average of the three scores above, recomputed on every Create/Update.</summary>
    public decimal OverallScore { get; set; }

    public string? Notes { get; set; }
}
