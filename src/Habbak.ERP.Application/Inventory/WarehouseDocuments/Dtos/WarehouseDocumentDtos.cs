namespace Habbak.ERP.Application.Inventory.WarehouseDocuments.Dtos;

public sealed class WarehouseDocumentListItemDto
{
    public required long Id { get; init; }
    public required string DocumentNumber { get; init; }
    public required DateOnly DocumentDate { get; init; }
    public string? SourceWarehouseCode { get; init; }
    public string? DestinationWarehouseCode { get; init; }
    public string? CustodyOfficerCode { get; init; }
    public required int LineCount { get; init; }
    public required string Status { get; init; }
}

public sealed class WarehouseDocumentDetailDto
{
    public required long Id { get; init; }
    public required string DocumentType { get; init; }
    public long? BranchId { get; init; }
    public required string DocumentNumber { get; init; }
    public required DateOnly DocumentDate { get; init; }
    public long? SourceWarehouseId { get; init; }
    public long? DestinationWarehouseId { get; init; }
    public long? CustodyOfficerId { get; init; }
    public long? RelatedWarehouseDocumentId { get; init; }
    public string? RelatedWarehouseDocumentNumber { get; init; }
    public required string Status { get; init; }
    public string? Notes { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT/Post for optimistic concurrency (00-Frontend-Specs.md, section 7).</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<WarehouseDocumentLineDto> Lines { get; init; }
}

public sealed class WarehouseDocumentLineDto
{
    public long? Id { get; init; }
    public required int LineNumber { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitCost { get; init; }

    /// <summary>The unit Quantity and UnitCost are in; UnitFactor base units make one of it.</summary>
    public required long UnitId { get; init; }
    public string? UnitCode { get; init; }
    public string? UnitNameAr { get; init; }
    public required decimal UnitFactor { get; init; }
    public string? BatchNumber { get; init; }
    public DateOnly? ExpiryDate { get; init; }

    /// <summary>TransferReceipt only — the quantity the related TransferOrder line shipped.</summary>
    public decimal? ExpectedQuantity { get; init; }

    /// <summary>TransferReceipt only — Quantity - ExpectedQuantity (rule 7).</summary>
    public decimal? VarianceQuantity { get; init; }
}

/// <summary>Quantity and UnitCost are in UnitId — the item's base unit when left out.</summary>
public sealed record WarehouseDocumentLineInput(
    long ItemId, decimal Quantity, decimal UnitCost, string? BatchNumber, DateOnly? ExpiryDate, long? UnitId = null);

/// <summary>TransferReceipt's own line input — Quantity is what actually arrived; UnitCost and
/// ExpectedQuantity are carried over from the matching TransferOrder line, not entered by hand.</summary>
public sealed record TransferReceiptLineInput(long ItemId, decimal Quantity, string? BatchNumber, DateOnly? ExpiryDate);
