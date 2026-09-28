namespace Habbak.ERP.Application.HR.EmployeeCertifications.Dtos;

public sealed class EmployeeCertificationDto
{
    public required long Id { get; init; }
    public required long EmployeeId { get; init; }
    public long? BranchId { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required string Issuer { get; init; }
    public required DateOnly IssueDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public string? CertificateNumber { get; init; }
    public long? AttachmentId { get; init; }
}
