using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.FixedAssets;

/// <summary>
/// One month of an asset's depreciation (section 2.1.3), laid out when the asset is activated.
/// Posted by the month's DepreciationRun; cancelled, never deleted, when the asset is disposed of
/// before its end (rule 32).
/// </summary>
public class DepreciationSchedule : AuditableEntity
{
    public long FixedAssetId { get; set; }
    public FixedAsset? FixedAsset { get; set; }

    public int PeriodNumber { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    public decimal Amount { get; set; }
    public decimal AccumulatedAfter { get; set; }
    public decimal BookValueAfter { get; set; }

    public DepreciationScheduleStatus Status { get; set; } = DepreciationScheduleStatus.Scheduled;

    /// <summary>The run that picked this period up (Draft or Posted); cleared when that run is reversed.</summary>
    public long? DepreciationRunId { get; set; }
    public DepreciationRun? DepreciationRun { get; set; }

    public DateTime? PostedAtUtc { get; set; }
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public bool IsPosted => Status == DepreciationScheduleStatus.Posted;
}

/// <summary>
/// A month's depreciation for the whole company (section 2.1.4), posted as ONE entry (rule 22).
/// IdempotencyKey is derived from (company, year, month): a second run for a month that already has
/// one — the job retried, someone clicked twice — finds it instead of doubling the entry (rule 28).
/// </summary>
public class DepreciationRun : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public string RunNumber { get; set; } = null!;
    public DateOnly RunDate { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public Guid IdempotencyKey { get; set; }

    public DepreciationRunStatus Status { get; set; } = DepreciationRunStatus.Draft;
    public decimal TotalDepreciation { get; set; }
    public int AssetCount { get; set; }

    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public long? PostedByUserId { get; set; }

    public DateTime? ReversedAtUtc { get; set; }
    public long? ReversedByUserId { get; set; }
    public long? ReversalJournalEntryId { get; set; }
    public JournalEntry? ReversalJournalEntry { get; set; }

    public ICollection<DepreciationSchedule> Periods { get; set; } = new List<DepreciationSchedule>();
}
