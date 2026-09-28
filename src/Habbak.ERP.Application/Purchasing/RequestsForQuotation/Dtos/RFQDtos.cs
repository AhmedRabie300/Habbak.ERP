namespace Habbak.ERP.Application.Purchasing.RequestsForQuotation.Dtos;

public sealed class RFQListItemDto
{
    public required long Id { get; init; }
    public required string RFQNumber { get; init; }
    public required DateOnly RFQDate { get; init; }
    public required int LineCount { get; init; }
    public required int SupplierCount { get; init; }
    public required string Status { get; init; }
}

public sealed class RFQDetailDto
{
    public required long Id { get; init; }
    public required string RFQNumber { get; init; }
    public required DateOnly RFQDate { get; init; }
    public long? BranchId { get; init; }
    public long? PurchaseRequestId { get; init; }
    public string? PurchaseRequestNumber { get; init; }
    public required string Status { get; init; }
    public DateOnly? RequiredDate { get; init; }
    public string? Notes { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<RFQLineDto> Lines { get; init; }
    public required IReadOnlyList<RFQSupplierDto> Suppliers { get; init; }
}

public sealed class RFQLineDto
{
    public required long Id { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required long UnitId { get; init; }
    public required string UnitCode { get; init; }
}

public sealed class RFQSupplierDto
{
    public required long Id { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required string Status { get; init; }
    public DateOnly? ResponseDate { get; init; }
    public required IReadOnlyList<RFQSupplierQuoteDto> Quotes { get; init; }
}

public sealed class RFQSupplierQuoteDto
{
    public required long Id { get; init; }
    public required long RFQLineId { get; init; }
    public required decimal UnitPrice { get; init; }
    public decimal? DiscountPercentage { get; init; }
    public int? DeliveryDays { get; init; }
    public required DateOnly ValidUntil { get; init; }
    public required bool IsExpired { get; init; }
    public required bool IsSelected { get; init; }
    public string? Notes { get; init; }
}

public sealed record RFQLineInput(long ItemId, decimal Quantity, long UnitId);
