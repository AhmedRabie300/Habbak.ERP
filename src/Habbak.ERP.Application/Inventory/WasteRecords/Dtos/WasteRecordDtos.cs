namespace Habbak.ERP.Application.Inventory.WasteRecords.Dtos;

public sealed class WasteRecordListItemDto
{
    public required long Id { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseCode { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required DateOnly WasteDate { get; init; }
    public required string Reason { get; init; }
    public string? SourceDocumentType { get; init; }
}

public sealed class WasteRecordDetailDto
{
    public required long Id { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseCode { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required DateOnly WasteDate { get; init; }
    public required string Reason { get; init; }
    public string? SourceDocumentType { get; init; }
    public long? SourceDocumentId { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }
}
