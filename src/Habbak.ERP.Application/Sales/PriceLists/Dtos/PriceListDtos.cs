namespace Habbak.ERP.Application.Sales.PriceLists.Dtos;

public sealed record PriceListLineInput(long ItemId, decimal DineInPrice, decimal TakeawayPrice, decimal DeliveryPrice);

public sealed class PriceListLineDto
{
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal DineInPrice { get; init; }
    public required decimal TakeawayPrice { get; init; }
    public required decimal DeliveryPrice { get; init; }
}

public sealed class PriceListListItemDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required DateOnly EffectiveFromDate { get; init; }
    public DateOnly? EffectiveToDate { get; init; }
    public required int BranchCount { get; init; }
    public required int ItemCount { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class PriceListDetailDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required DateOnly EffectiveFromDate { get; init; }
    public DateOnly? EffectiveToDate { get; init; }
    public required bool IsActive { get; init; }
    public required IReadOnlyList<long> BranchIds { get; init; }
    public required IReadOnlyList<PriceListLineDto> Lines { get; init; }
}
