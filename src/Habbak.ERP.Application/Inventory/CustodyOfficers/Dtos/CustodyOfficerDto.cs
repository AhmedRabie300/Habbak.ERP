namespace Habbak.ERP.Application.Inventory.CustodyOfficers.Dtos;

public sealed class CustodyOfficerDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public long? BranchId { get; init; }
    public required bool IsActive { get; init; }
}
