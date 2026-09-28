namespace Habbak.ERP.Application.HR.OrgUnits.Dtos;

public sealed class OrgUnitDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public long? ParentId { get; init; }
    public long? BranchId { get; init; }
    public long? ManagerEmployeeId { get; init; }
    public long? CostCenterDimensionValueId { get; init; }
}
