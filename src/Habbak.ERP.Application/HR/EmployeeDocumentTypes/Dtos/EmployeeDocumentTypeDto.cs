namespace Habbak.ERP.Application.HR.EmployeeDocumentTypes.Dtos;

public sealed class EmployeeDocumentTypeDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public required bool RequiresExpiry { get; init; }
    public required bool IsMandatory { get; init; }
    public int? ExpiryAlertDays { get; init; }
}
