using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.Purchasing;

namespace Habbak.ERP.Domain.FixedAssets;

/// <summary>فئة صيانة (section 2.2.1).</summary>
public class MaintenanceCategory : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public MaintenanceType MaintenanceType { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A fault report (section 2.2.2) — for a registered asset or any other device, by name.</summary>
public class MaintenanceIssue : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string IssueNumber { get; set; } = null!;
    public long? FixedAssetId { get; set; }
    public FixedAsset? FixedAsset { get; set; }
    public string? DeviceName { get; set; }
    public DateTime ReportedAtUtc { get; set; }
    public long ReportedByUserId { get; set; }
    public string Description { get; set; } = null!;
    public IssueSeverity Severity { get; set; } = IssueSeverity.Medium;
    public MaintenanceIssueStatus Status { get; set; } = MaintenanceIssueStatus.Reported;
    public string? Notes { get; set; }
}

/// <summary>
/// A maintenance job (section 2.2.3). Its cost is never typed in: ActualCost = LaborCost + the spare
/// parts, and a stocked part is costed at the warehouse's average when issued (rule 16). Completing
/// it posts up to two entries (rule 20): the external cost against the supplier/treasury, and the
/// stocked parts against inventory — never one lumped line.
/// </summary>
public class MaintenanceRequest : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public string RequestNumber { get; set; } = null!;
    public long? IssueId { get; set; }
    public MaintenanceIssue? Issue { get; set; }
    public long FixedAssetId { get; set; }
    public FixedAsset? FixedAsset { get; set; }
    public long MaintenanceCategoryId { get; set; }
    public MaintenanceCategory? MaintenanceCategory { get; set; }

    /// <summary>Set on the requests the preventive-maintenance job raises; completing one moves the schedule on.</summary>
    public long? MaintenanceScheduleId { get; set; }
    public MaintenanceSchedule? MaintenanceSchedule { get; set; }
    public DateOnly? DueDate { get; set; }

    /// <summary>Required only for job-raised requests: derived from (schedule, due date) — one request per due date (rule 29).</summary>
    public Guid? IdempotencyKey { get; set; }

    public DateOnly RequestDate { get; set; }
    public DateOnly? ScheduledDate { get; set; }
    public DateOnly? CompletedDate { get; set; }

    /// <summary>
    /// The technician, once matched to a real employee (Phase 1.2, exact-name auto-match or manual) —
    /// <see cref="TechnicianName"/> is kept as it was regardless, so old requests stay readable as-is.
    /// </summary>
    public long? TechnicianId { get; set; }
    public Employee? Technician { get; set; }
    public string? TechnicianName { get; set; }

    public long? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>What the external cost (labor, parts bought for the job) is credited to: supplier payable or treasury.</summary>
    public long? ExternalCreditAccountId { get; set; }
    public Account? ExternalCreditAccount { get; set; }

    public MaintenanceRequestStatus Status { get; set; } = MaintenanceRequestStatus.Draft;

    /// <summary>When it was approved — kept once the status moves on, for the cost threshold (rule 15).</summary>
    public DateTime? ApprovedAtUtc { get; set; }

    public decimal? EstimatedCost { get; set; }
    public decimal LaborCost { get; set; }

    /// <summary>Σ spare parts — stocked (at average cost) and bought for the job.</summary>
    public decimal SparePartsTotalCost { get; set; }

    /// <summary>LaborCost + SparePartsTotalCost.</summary>
    public decimal ActualCost { get; set; }

    public string? Notes { get; set; }

    /// <summary>The external-cost entry (labor + parts bought for the job).</summary>
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    /// <summary>The stocked-parts entry.</summary>
    public long? SparePartsJournalEntryId { get; set; }
    public JournalEntry? SparePartsJournalEntry { get; set; }

    public ICollection<MaintenanceSparePart> SpareParts { get; set; } = new List<MaintenanceSparePart>();
}

/// <summary>
/// A part used on a maintenance job (section 2.2.4). A stocked part (ItemId) comes out of WarehouseId
/// at the average cost of the moment it is issued — never a typed price; a part bought for the job
/// has no item and its cost is typed in.
/// </summary>
public class MaintenanceSparePart : AuditableEntity
{
    public long MaintenanceRequestId { get; set; }
    public MaintenanceRequest? MaintenanceRequest { get; set; }
    public long? ItemId { get; set; }
    public Item? Item { get; set; }
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public long? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public long? StockTransactionId { get; set; }
    public StockTransaction? StockTransaction { get; set; }

    public bool IsStocked => ItemId is not null;
}

/// <summary>Preventive maintenance on a timetable (section 2.2.5); the daily job raises a request when it falls due.</summary>
public class MaintenanceSchedule : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public long FixedAssetId { get; set; }
    public FixedAsset? FixedAsset { get; set; }
    public long MaintenanceCategoryId { get; set; }
    public MaintenanceCategory? MaintenanceCategory { get; set; }
    public MaintenanceFrequency Frequency { get; set; }
    public DateOnly? LastExecutedDate { get; set; }
    public DateOnly NextDueDate { get; set; }

    /// <summary>The technician, once matched to a real employee (Phase 1.2) — see <see cref="MaintenanceRequest.TechnicianId"/>.</summary>
    public long? TechnicianId { get; set; }
    public Employee? Technician { get; set; }
    public string? TechnicianName { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    /// <summary>The due date after <paramref name="from"/>.</summary>
    public static DateOnly Advance(DateOnly from, MaintenanceFrequency frequency) => frequency switch
    {
        MaintenanceFrequency.Daily => from.AddDays(1),
        MaintenanceFrequency.Weekly => from.AddDays(7),
        MaintenanceFrequency.Monthly => from.AddMonths(1),
        MaintenanceFrequency.Quarterly => from.AddMonths(3),
        MaintenanceFrequency.SemiAnnual => from.AddMonths(6),
        _ => from.AddYears(1)
    };
}
