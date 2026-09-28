using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Organization;

namespace Habbak.ERP.Domain.HR;

/// <summary>
/// 1:1 with Employee, separate table for independent field-permission/audit scope (Docs/Modules/
/// 10-Module-HR-Payroll.md §2.1, §5.5) — modeled as FK + filtered unique index (the
/// BranchPOSSettings pattern), since no shared-primary-key precedent exists in this codebase
/// (Phase-1.1-Research.md §2.3). NationalIdEncrypted/BankIbanEncrypted are the first real use of
/// EncryptedStringConverter (0.3); NationalIdHash is the first real use of IPiiHasher (0.6).
/// [PiiField] properties must all be registered in FieldPermissionCatalog (0.4) — done in the same
/// batch this entity was added, not deferred. BankId/NationalityId/CityId/MilitaryStatusId/
/// QualificationTypeId/EmergencyContactRelationshipTypeId are lookup references, not PII themselves.
/// </summary>
public class EmployeePersonalData : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    [PiiField(Reason = "National ID — encrypted at rest, revealed only via HR_REVEAL_PII (1.1b)")]
    public string NationalIdEncrypted { get; set; } = null!;

    [PiiField(Reason = "HMAC of the National ID — used for uniqueness/lookup, derived from PII")]
    public string NationalIdHash { get; set; } = null!;

    /// <summary>Safe to display as-is (•••• + this) — deliberately not [PiiField].</summary>
    public string NationalIdLast4 { get; set; } = null!;

    [PiiField(Reason = "Bank IBAN — encrypted at rest, revealed only via HR_REVEAL_PII (1.1b)")]
    public string? BankIbanEncrypted { get; set; }

    /// <summary>Safe to display as-is — deliberately not [PiiField].</summary>
    public string? BankIbanLast4 { get; set; }
    public string? BankName { get; set; }

    public long? BankId { get; set; }
    public Bank? Bank { get; set; }

    public long? NationalityId { get; set; }
    public Nationality? Nationality { get; set; }

    public long? CityId { get; set; }
    public City? City { get; set; }

    public long? MilitaryStatusId { get; set; }
    public MilitaryStatus? MilitaryStatus { get; set; }

    public long? QualificationTypeId { get; set; }
    public QualificationType? QualificationType { get; set; }

    [PiiField(Reason = "Date of birth")]
    public DateOnly BirthDate { get; set; }

    public Gender Gender { get; set; }
    public MaritalStatus MaritalStatus { get; set; }

    [PiiField(Reason = "Home address")]
    public string? Address { get; set; }

    [PiiField(Reason = "Personal phone number")]
    public string? PhoneNumber { get; set; }

    [PiiField(Reason = "Personal email")]
    public string? PersonalEmail { get; set; }

    [PiiField(Reason = "Emergency contact name")]
    public string? EmergencyContactName { get; set; }

    [PiiField(Reason = "Emergency contact phone number")]
    public string? EmergencyContactPhone { get; set; }

    public long? EmergencyContactRelationshipTypeId { get; set; }
    public RelationshipType? EmergencyContactRelationshipType { get; set; }
}
