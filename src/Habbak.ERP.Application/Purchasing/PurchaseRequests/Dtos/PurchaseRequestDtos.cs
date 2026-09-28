namespace Habbak.ERP.Application.Purchasing.PurchaseRequests.Dtos;

public sealed class PurchaseRequestListItemDto
{
    public required long Id { get; init; }
    public required string RequestNumber { get; init; }
    public required DateOnly RequestDate { get; init; }
    public long? BranchId { get; init; }
    public required string Priority { get; init; }
    public required int LineCount { get; init; }
    public required string Status { get; init; }
}

public sealed class PurchaseRequestDetailDto
{
    public required long Id { get; init; }
    public required string RequestNumber { get; init; }
    public required DateOnly RequestDate { get; init; }
    public long? BranchId { get; init; }
    public required long RequestedByUserId { get; init; }
    public required string Priority { get; init; }
    public string? Reason { get; init; }
    public required string Status { get; init; }
    public string? Notes { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<PurchaseRequestLineDto> Lines { get; init; }
}

public sealed class PurchaseRequestLineDto
{
    public long? Id { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required long UnitId { get; init; }
    public required string UnitCode { get; init; }

    /// <summary>Base units in one UnitId, and the line's quantity in base units.</summary>
    public required decimal UnitFactor { get; init; }
    public required decimal BaseQuantity { get; init; }
    public string? Notes { get; init; }
}

/// <summary>Quantity is in UnitId — the item's base unit when left out.</summary>
public sealed record PurchaseRequestLineInput(long ItemId, decimal Quantity, long? UnitId, string? Notes);
