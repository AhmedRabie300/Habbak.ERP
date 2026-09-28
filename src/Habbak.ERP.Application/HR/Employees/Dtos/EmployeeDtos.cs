using Habbak.ERP.Domain.HR;

namespace Habbak.ERP.Application.HR.Employees.Dtos;

public sealed class EmployeeListItemDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public long? BranchId { get; init; }
    public required long OrgUnitId { get; init; }
    public required long JobPositionId { get; init; }
    public required long JobGradeId { get; init; }
    public long? ManagerId { get; init; }
    public long? UserId { get; init; }
    public required DateOnly HireDate { get; init; }
    public required EmploymentType EmploymentType { get; init; }
    public required EmployeeStatus Status { get; init; }
}

/// <summary>
/// Deliberately excludes NationalIdEncrypted/BankIbanEncrypted (only *Last4) — full plaintext is only
/// ever returned by RevealPiiFieldCommand (§1.1b), audited every time. The other [PiiField] properties
/// (BirthDate, Address, PhoneNumber, PersonalEmail, EmergencyContact*) ARE included here and rely on
/// EmployeesController's [MaskFields("EmployeePersonalData")] to null them out for viewers without the
/// field's View permission — the pre-existing Phase 0.4 mechanism, not something new for B5.
/// </summary>
public sealed class EmployeePersonalDataDto
{
    public required long Id { get; init; }
    public required string NationalIdLast4 { get; init; }
    public string? BankIbanLast4 { get; init; }
    public string? BankName { get; init; }
    public long? BankId { get; init; }
    public long? NationalityId { get; init; }
    public long? CityId { get; init; }
    public long? MilitaryStatusId { get; init; }
    public long? QualificationTypeId { get; init; }
    public required DateOnly BirthDate { get; init; }
    public required Gender Gender { get; init; }
    public required MaritalStatus MaritalStatus { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
    public string? PersonalEmail { get; init; }
    public string? EmergencyContactName { get; init; }
    public string? EmergencyContactPhone { get; init; }
    public long? EmergencyContactRelationshipTypeId { get; init; }
}

public sealed class EmployeeDetailDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public long? BranchId { get; init; }
    public required long OrgUnitId { get; init; }
    public required long JobPositionId { get; init; }
    public required long JobGradeId { get; init; }
    public long? ManagerId { get; init; }
    public long? UserId { get; init; }
    public required DateOnly HireDate { get; init; }
    public required EmploymentType EmploymentType { get; init; }
    public required EmployeeStatus Status { get; init; }
    public long? CostCenterDimensionValueId { get; init; }
    public DateOnly? TerminationDate { get; init; }
    public EmployeePersonalDataDto? PersonalData { get; init; }
}

public sealed class EmployeeLookupDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
}
