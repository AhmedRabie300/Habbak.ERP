namespace Habbak.ERP.Application.HR.JobPositions.Dtos;

public sealed class JobPositionDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public long? OrgUnitId { get; init; }
    public long? DefaultJobGradeId { get; init; }
}
