namespace Habbak.ERP.Application.Sales.SalesReturns.Dtos;

public sealed record SalesReturnLineInput(long ItemId, decimal Quantity, decimal UnitPrice, string? BatchNumber);

public sealed class SalesReturnLineDto
{
    public long? Id { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public string? BatchNumber { get; init; }
}

public sealed class SalesReturnListItemDto
{
    public required long Id { get; init; }
    public required string ReturnNumber { get; init; }
    public required DateOnly ReturnDate { get; init; }
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required string CustomerNameAr { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseNameAr { get; init; }
    public long? SourceInvoiceId { get; init; }
    public required string Reason { get; init; }
    public required int LineCount { get; init; }
    public required string Status { get; init; }
}

public sealed class SalesReturnDetailDto
{
    public required long Id { get; init; }
    public required string ReturnNumber { get; init; }
    public required DateOnly ReturnDate { get; init; }
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required string CustomerNameAr { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseNameAr { get; init; }
    public long? SourceInvoiceId { get; init; }
    public string? SourceInvoiceNumber { get; init; }
    public required string Reason { get; init; }
    public required string Status { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<SalesReturnLineDto> Lines { get; init; }
}
