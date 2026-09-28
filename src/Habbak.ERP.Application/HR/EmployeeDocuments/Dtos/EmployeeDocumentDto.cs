namespace Habbak.ERP.Application.HR.EmployeeDocuments.Dtos;

public sealed class EmployeeDocumentDto
{
    public required long Id { get; init; }
    public required long EmployeeId { get; init; }
    public long? BranchId { get; init; }
    public required long EmployeeDocumentTypeId { get; init; }
    public required DateOnly IssueDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public required long AttachmentId { get; init; }
    public string? DocumentNumber { get; init; }
}
