namespace Habbak.ERP.Application.Inventory.BranchRequests.Dtos;

public sealed class BranchRequestListItemDto
{
    public required long Id { get; init; }
    public required string RequestNumber { get; init; }
    public required DateOnly RequestDate { get; init; }
    public long? BranchId { get; init; }
    public required int LineCount { get; init; }
    public required string Status { get; init; }
}

public sealed class BranchRequestDetailDto
{
    public required long Id { get; init; }
    public long? BranchId { get; init; }
    public required string RequestNumber { get; init; }
    public required DateOnly RequestDate { get; init; }
    public required long RequestedByUserId { get; init; }
    public required string Status { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency (00-Frontend-Specs.md, section 7).</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<BranchRequestLineDto> Lines { get; init; }
}

public sealed class BranchRequestLineDto
{
    public long? Id { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal RequestedQuantity { get; init; }
    public decimal? ApprovedQuantity { get; init; }

    /// <summary>The unit both quantities are in; UnitFactor base units make one of it.</summary>
    public required long UnitId { get; init; }
    public string? UnitCode { get; init; }
    public string? UnitNameAr { get; init; }
    public required decimal UnitFactor { get; init; }

    /// <summary>From BranchItemLimit for the same (BranchId, ItemId) if one exists — display-only,
    /// lets the interactive create/submit screen (screen #12) show the limit as the user types.</summary>
    public decimal? MinRequestQuantity { get; init; }
    public decimal? MaxRequestQuantity { get; init; }
}

/// <summary>RequestedQuantity is in UnitId — the item's base unit when left out.</summary>
public sealed record BranchRequestLineInput(long ItemId, decimal RequestedQuantity, long? UnitId = null);

public sealed record ApproveBranchRequestLineInput(long LineId, decimal ApprovedQuantity);
