using System.Security.Claims;
using Habbak.ERP.Application.Common.Interfaces;

namespace Habbak.ERP.API.Auth;

/// <summary>
/// Reads CompanyId/BranchId/UserId from the authenticated request's JWT Claims
/// (00-Project-Overview.md, section 7). Correct as written, but only useful once the JWT
/// issuing/validation middleware that populates these claims is wired up — that is a separate,
/// not-yet-built work item. Until then, any handler that resolves this outside an authenticated
/// request fails with a clear exception rather than silently reading a wrong/default company.
///
/// Inside a background job's BackgroundCompanyScope there is no request: the job's company is
/// reported instead, company-wide (no branch limit), with the system (user 0) acting.
/// </summary>
public class CurrentCompanyContext(IHttpContextAccessor httpContextAccessor) : ICurrentCompanyContext
{
    private ClaimsPrincipal User =>
        httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("No active HTTP request to resolve the current company context from.");

    public long CompanyId =>
        BackgroundCompanyScope.CompanyId is { } jobCompany ? jobCompany :
        long.TryParse(User.FindFirstValue("CompanyId"), out var companyId)
            ? companyId
            : throw new InvalidOperationException("The current request has no valid CompanyId claim.");

    public long? BranchId =>
        BackgroundCompanyScope.CompanyId is not null ? null :
        long.TryParse(User.FindFirstValue("BranchId"), out var branchId) ? branchId : null;

    public long UserId =>
        BackgroundCompanyScope.CompanyId is not null ? 0 :
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId"), out var userId)
            ? userId
            : throw new InvalidOperationException("The current request has no valid UserId claim.");

    public long? EmployeeId =>
        BackgroundCompanyScope.CompanyId is not null ? null :
        long.TryParse(User.FindFirstValue("EmployeeId"), out var employeeId) ? employeeId : null;
}
