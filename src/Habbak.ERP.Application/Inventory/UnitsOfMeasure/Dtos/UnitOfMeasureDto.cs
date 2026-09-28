namespace Habbak.ERP.Application.Inventory.UnitsOfMeasure.Dtos;

public sealed class UnitOfMeasureDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required string Category { get; init; }
    public required bool IsActive { get; init; }
}
