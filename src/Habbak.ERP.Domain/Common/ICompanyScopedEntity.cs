namespace Habbak.ERP.Domain.Common;

/// <summary>
/// Marks an entity as carrying a CompanyId column, so the Infrastructure layer can apply the
/// Global Query Filter generically via reflection instead of one filter per entity
/// (00-Project-Overview.md, section 3).
/// </summary>
public interface ICompanyScopedEntity
{
    long? CompanyId { get; set; }
}

/// <summary>
/// Marks an entity as carrying a direct (nullable) BranchId column — as opposed to branch
/// attribution via an analytical dimension. See 01-Module-Accounting.md, rule 25.
/// </summary>
public interface IBranchScopedEntity
{
    long? BranchId { get; set; }
}

/// <summary>
/// Marks an entity as belonging to exactly one employee — unlike branch (optional, company-wide rows
/// allowed), a row like an employment contract or a document always belongs to one employee, so this
/// is non-nullable. The Infrastructure layer applies the <see cref="DataScope.Self"/> Global Query
/// Filter to it generically by reflection, the same way it already does for
/// <see cref="IBranchScopedEntity"/> (Docs/Implementation/HR-Core-Plan.md §0.1). No entity implements
/// this yet — it is ready for the HR module's own entities (EmploymentContract, EmployeeDocument,
/// EmployeeCertification) once they exist.
/// </summary>
public interface IEmployeeScopedEntity
{
    long EmployeeId { get; }
}
