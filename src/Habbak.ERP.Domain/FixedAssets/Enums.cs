namespace Habbak.ERP.Domain.FixedAssets;

// Docs/Modules/08-Module-Maintenance-FixedAssets-Request.md, section 2.4.

public enum DepreciationMethod
{
    StraightLine = 1,

    /// <summary>A fixed annual rate on the remaining book value — needs DepreciationRate (rule 4).</summary>
    DecliningBalance = 2,

    NoDepreciation = 3
}

public enum FixedAssetStatus
{
    /// <summary>
    /// Registered but not yet capitalised: no acquisition entry and no depreciation schedule, and its
    /// cost can still change. Activating it posts the acquisition and freezes the cost (rules 8-10).
    /// Not in the spec's list — it is what "after approval" in rules 8 and 9 needs a state for.
    /// </summary>
    Draft = 1,

    Active = 2,
    InMaintenance = 3,

    /// <summary>On its way to another branch: a transfer is drafted for it and not yet posted.</summary>
    Transferred = 4,

    Disposed = 5,
    WrittenOff = 6
}

public enum DisposalType
{
    Sale = 1,
    Scrap = 2,
    Loss = 3
}

public enum AssetTransferStatus
{
    Draft = 1,
    Posted = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum AssetDisposalStatus
{
    Draft = 1,
    Posted = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum AssetPhysicalCountStatus
{
    Draft = 1,
    InProgress = 2,
    Completed = 3,
    Rejected = 4
}

public enum AssetCondition
{
    Good = 1,
    Fair = 2,
    Damaged = 3
}

public enum AssetPhysicalCountFrequency
{
    Annual = 1,
    SemiAnnual = 2,
    Quarterly = 3
}

public enum DepreciationScheduleStatus
{
    Scheduled = 1,
    Posted = 2,

    /// <summary>A future period of an asset disposed of before the end of its life (rule 32) — kept, never posted.</summary>
    Cancelled = 3
}

public enum DepreciationRunStatus
{
    Draft = 1,
    Posted = 2,
    Reversed = 3
}

public enum MaintenanceType
{
    Preventive = 1,
    Corrective = 2,
    Inspection = 3
}

public enum IssueSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum MaintenanceIssueStatus
{
    Reported = 1,
    UnderInspection = 2,
    Repairing = 3,
    Repaired = 4,
    Rejected = 5,
    Cancelled = 6
}

public enum MaintenanceRequestStatus
{
    Draft = 1,
    Approved = 2,
    InProgress = 3,
    Completed = 4,
    Rejected = 5,
    Cancelled = 6
}

public enum MaintenanceFrequency
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Quarterly = 4,
    SemiAnnual = 5,
    Annual = 6
}
