namespace Habbak.ERP.Application.Inventory.Warehouses.Dtos;

public sealed class WarehouseDto
{
    public required long Id { get; init; }
    public long? BranchId { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required string WarehouseType { get; init; }
    public required bool AllowNegativeBalance { get; init; }
    public required bool IsActive { get; init; }
}
