namespace Habbak.ERP.Application.Sales.SalesOrders.Dtos;

public sealed record SalesOrderLineInput(long ItemId, decimal Quantity, decimal UnitPrice);

public sealed class SalesOrderLineDto
{
    public long? Id { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal LineTotal { get; init; }
    public required decimal DeliveredQuantity { get; init; }
}

public sealed class SalesOrderListItemDto
{
    public required long Id { get; init; }
    public required string OrderNumber { get; init; }
    public required DateOnly OrderDate { get; init; }
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required string CustomerNameAr { get; init; }
    public long? SourceQuoteId { get; init; }
    public required decimal Subtotal { get; init; }
    public required string Status { get; init; }
}

public sealed class SalesOrderDetailDto
{
    public required long Id { get; init; }
    public required string OrderNumber { get; init; }
    public required DateOnly OrderDate { get; init; }
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required string CustomerNameAr { get; init; }
    public long? SourceQuoteId { get; init; }
    public string? SourceQuoteNumber { get; init; }
    public required decimal Subtotal { get; init; }
    public required string Status { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<SalesOrderLineDto> Lines { get; init; }
}
