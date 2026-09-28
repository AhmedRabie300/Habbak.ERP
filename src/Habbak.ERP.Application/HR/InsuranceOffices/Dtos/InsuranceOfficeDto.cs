namespace Habbak.ERP.Application.HR.InsuranceOffices.Dtos;

public sealed class InsuranceOfficeDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public string? OfficialCode { get; init; }
    public string? Address { get; init; }
}
