namespace Habbak.ERP.Application.Sales.Discounts.Dtos;

public sealed class DiscountListItemDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required string DiscountType { get; init; }
    public required decimal Value { get; init; }
    public required int ApplicationPriority { get; init; }
    public required bool IsStackable { get; init; }
    public required bool IsHappyHour { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class DiscountDetailDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required string DiscountType { get; init; }
    public required decimal Value { get; init; }
    public required int ApplicationPriority { get; init; }
    public required bool IsStackable { get; init; }
    public decimal? MinInvoiceAmount { get; init; }
    public decimal? MinQuantity { get; init; }
    public required bool IsHappyHour { get; init; }
    public TimeOnly? HappyHourFromTime { get; init; }
    public TimeOnly? HappyHourToTime { get; init; }
    public required DateOnly EffectiveFromDate { get; init; }
    public DateOnly? EffectiveToDate { get; init; }
    public required bool IsActive { get; init; }
}
