namespace Habbak.ERP.Application.Purchasing.SupplierContracts.Dtos;

public sealed class SupplierContractListItemDto
{
    public required long Id { get; init; }
    public required string ContractNumber { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required int ItemCount { get; init; }
    public required string Status { get; init; }
}

public sealed class SupplierContractDetailDto
{
    public required long Id { get; init; }
    public required string ContractNumber { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required bool AutoRenew { get; init; }
    public required string Status { get; init; }
    public string? Notes { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<ContractItemDto> Items { get; init; }
}

public sealed class ContractItemDto
{
    public long? Id { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal UnitPrice { get; init; }
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public decimal? DiscountPercentage { get; init; }
}

public sealed record ContractItemInput(long ItemId, decimal UnitPrice, decimal? MinQuantity, decimal? MaxQuantity, decimal? DiscountPercentage);
