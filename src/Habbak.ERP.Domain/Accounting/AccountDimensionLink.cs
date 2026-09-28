using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// Links an Account to one of its analytical dimensions. A single account may carry at most
/// 5 links (rule 24, enforced by the Application layer / DB check, since it is a cross-row
/// invariant that cannot be expressed as a column constraint).
/// (01-Module-Accounting.md, section 2.1).
/// </summary>
public class AccountDimensionLink : AuditableEntity
{
    public long AccountId { get; set; }
    public Account Account { get; set; } = default!;

    public long CostCenterDimensionId { get; set; }
    public CostCenterDimension CostCenterDimension { get; set; } = default!;

    /// <summary>Display order of this dimension's column on the journal entry screen (1-5).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// When true, posting any line that uses this account without a value for this dimension
    /// is rejected (rule 3).
    /// </summary>
    public bool IsMandatory { get; set; }
}
