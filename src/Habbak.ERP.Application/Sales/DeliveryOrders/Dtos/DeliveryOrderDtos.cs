namespace Habbak.ERP.Application.Sales.DeliveryOrders.Dtos;

public sealed record DeliveryOrderLineInput(long ItemId, decimal Quantity, string? BatchNumber);

public sealed class DeliveryOrderLineDto
{
    public long? Id { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public string? BatchNumber { get; init; }
}

public sealed class DeliveryOrderListItemDto
{
    public required long Id { get; init; }
    public required string DeliveryNumber { get; init; }
    public required DateOnly DeliveryDate { get; init; }
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required string CustomerNameAr { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseNameAr { get; init; }
    public long? SourceOrderId { get; init; }
    public long? SourceInvoiceId { get; init; }
    public required int LineCount { get; init; }
    public required string Status { get; init; }
}

public sealed class DeliveryOrderDetailDto
{
    public required long Id { get; init; }
    public required string DeliveryNumber { get; init; }
    public required DateOnly DeliveryDate { get; init; }
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required string CustomerNameAr { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseNameAr { get; init; }
    public long? SourceOrderId { get; init; }
    public string? SourceOrderNumber { get; init; }
    public long? SourceInvoiceId { get; init; }
    public string? SourceInvoiceNumber { get; init; }
    public required string Status { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<DeliveryOrderLineDto> Lines { get; init; }
}
