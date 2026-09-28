namespace Habbak.ERP.Application.Inventory.ItemGroups.Dtos;

public sealed class ItemGroupDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public long? ParentId { get; init; }
    public required bool IsActive { get; init; }
}
