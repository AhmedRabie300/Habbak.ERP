namespace Habbak.ERP.Application.Sales.SalesQuotes.Dtos;

public sealed record SalesQuoteLineInput(long ItemId, decimal Quantity, decimal UnitPrice, decimal? DiscountAmount);

public sealed class SalesQuoteLineDto
{
    public long? Id { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public decimal? DiscountAmount { get; init; }
    public required decimal LineTotal { get; init; }
}

public sealed class SalesQuoteListItemDto
{
    public required long Id { get; init; }
    public required string QuoteNumber { get; init; }
    public required DateOnly QuoteDate { get; init; }
    public required DateOnly ValidUntil { get; init; }
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required string CustomerNameAr { get; init; }
    public required decimal Subtotal { get; init; }
    public required string Status { get; init; }
}

public sealed class SalesQuoteDetailDto
{
    public required long Id { get; init; }
    public required string QuoteNumber { get; init; }
    public required DateOnly QuoteDate { get; init; }
    public required DateOnly ValidUntil { get; init; }
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required string CustomerNameAr { get; init; }
    public required decimal Subtotal { get; init; }
    public required string Status { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<SalesQuoteLineDto> Lines { get; init; }
}

/// <summary>Feeds screen #6's "convert to sales order" picker.</summary>
public sealed class AcceptedSalesQuoteListItemDto
{
    public required long Id { get; init; }
    public required string QuoteNumber { get; init; }
    public required long CustomerId { get; init; }
    public required string CustomerNameAr { get; init; }
    public required DateOnly ValidUntil { get; init; }
    public required decimal Subtotal { get; init; }
}
