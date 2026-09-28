using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// Stands in for real JWT validation in tests.
/// A test authenticates as whatever CompanyId/UserId it puts in these headers, letting tests
/// exercise the Global Query Filter's company isolation over real HTTP without a login endpoint.
/// </summary>
public class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string CompanyIdHeader = "X-Test-CompanyId";
    public const string UserIdHeader = "X-Test-UserId";

    /// <summary>Optional; mirrors the real JwtAccessTokenIssuer's EmployeeId claim for a user linked to an Employee.</summary>
    public const string EmployeeIdHeader = "X-Test-EmployeeId";

    /// <summary>Comma-separated role codes; SUPER_ADMIN when absent, so a test that is not about permissions is never stopped by them.</summary>
    public const string RolesHeader = "X-Test-Roles";

    /// <summary>Any value marks the token as "password change required", like a real login does.</summary>
    /// <summary>A branch id limits the session to that branch, like a login through a branch scope.</summary>
    public const string BranchIdHeader = "X-Test-BranchId";

    public const string PasswordChangeHeader = "X-Test-PasswordChangeRequired";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var companyId = Request.Headers[CompanyIdHeader].FirstOrDefault() ?? "1";
        var userId = Request.Headers[UserIdHeader].FirstOrDefault() ?? "1";

        var roles = (Request.Headers[RolesHeader].FirstOrDefault() ?? "SUPER_ADMIN")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var claims = new List<Claim>
        {
            new("CompanyId", companyId),
            new("UserId", userId),
            new(ClaimTypes.NameIdentifier, userId)
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        if (Request.Headers[BranchIdHeader].FirstOrDefault() is { Length: > 0 } branchId)
        {
            claims.Add(new Claim("BranchId", branchId));
        }

        if (Request.Headers[EmployeeIdHeader].FirstOrDefault() is { Length: > 0 } employeeId)
        {
            claims.Add(new Claim("EmployeeId", employeeId));
        }

        if (Request.Headers.ContainsKey(PasswordChangeHeader))
        {
            claims.Add(new Claim("pwd_change_required", "true"));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
