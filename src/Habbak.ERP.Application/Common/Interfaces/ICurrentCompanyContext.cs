namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>
/// Resolves the current request's CompanyId/BranchId/UserId from the auth Claims
/// (00-Project-Overview.md, section 3 and 7). Consumed by EF Core Global Query Filters
/// and by the second-line company-leak guard at the API layer.
/// </summary>
public interface ICurrentCompanyContext
{
    long CompanyId { get; }
    long? BranchId { get; }
    long UserId { get; }

    /// <summary>
    /// The Employee linked to the current user (Employee.UserId, once the HR module exists) — null
    /// until then. Read by AppDbContext.CurrentEmployeeId for the <see cref="Habbak.ERP.Domain.Common.IEmployeeScopedEntity"/>
    /// Global Query Filter (Docs/Implementation/HR-Core-Plan.md §0.1).
    /// </summary>
    long? EmployeeId { get; }
}
