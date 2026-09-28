namespace Habbak.ERP.Application.Organization.Branches.Dtos;

public sealed class BranchDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
}
