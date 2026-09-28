namespace Habbak.ERP.Application.HR.JobGrades.Dtos;

public sealed class JobGradeDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public required int Level { get; init; }
    public decimal? MinSalary { get; init; }
    public decimal? MaxSalary { get; init; }
}
