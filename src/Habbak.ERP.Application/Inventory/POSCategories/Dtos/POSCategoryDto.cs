namespace Habbak.ERP.Application.Inventory.POSCategories.Dtos;

public sealed class POSCategoryDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required int DisplayOrder { get; init; }
    public required bool IsActive { get; init; }
}
