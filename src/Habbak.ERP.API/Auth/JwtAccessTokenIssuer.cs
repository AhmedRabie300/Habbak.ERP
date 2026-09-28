using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Habbak.ERP.Application.Common.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Habbak.ERP.API.Auth;

/// <summary>Claim names this API puts in (and reads back from) its access tokens.</summary>
public static class AuthClaims
{
    public const string CompanyId = "CompanyId";
    public const string BranchId = "BranchId";
    public const string UserId = "UserId";

    /// <summary>Null/absent until the HR module's Employee.UserId link exists (Docs/Implementation/HR-Core-Plan.md §0.1).</summary>
    public const string EmployeeId = "EmployeeId";
    public const string Role = "role";
    public const string PasswordChangeRequired = "pwd_change_required";
}

/// <summary>HMAC-SHA256 JWTs signed with the key in configuration section "Jwt".</summary>
public sealed class JwtAccessTokenIssuer(IConfiguration configuration) : IAccessTokenIssuer
{
    public IssuedAccessToken Issue(AccessTokenSubject subject, TimeSpan lifetime)
    {
        var jwt = configuration.GetSection("Jwt");
        var claims = new List<Claim>
        {
            new(AuthClaims.CompanyId, subject.CompanyId.ToString()),
            new(AuthClaims.UserId, subject.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, subject.UserId.ToString()),
            new(ClaimTypes.Name, subject.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        if (subject.BranchId is not null)
        {
            claims.Add(new Claim(AuthClaims.BranchId, subject.BranchId.Value.ToString()));
        }

        if (subject.EmployeeId is not null)
        {
            claims.Add(new Claim(AuthClaims.EmployeeId, subject.EmployeeId.Value.ToString()));
        }

        claims.AddRange(subject.RoleCodes.Select(code => new Claim(AuthClaims.Role, code)));

        if (subject.PasswordChangeRequired)
        {
            claims.Add(new Claim(AuthClaims.PasswordChangeRequired, "true"));
        }

        var expires = DateTime.UtcNow.Add(lifetime);
        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)), SecurityAlgorithms.HmacSha256));

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

public sealed class HttpRequestInfo(IHttpContextAccessor accessor) : IRequestInfo
{
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
}

public sealed class ClaimsUserRoles(IHttpContextAccessor accessor) : ICurrentUserRoles
{
    public IReadOnlyList<string> RoleCodes =>
        accessor.HttpContext?.User.Claims
            .Where(c => c.Type is AuthClaims.Role or ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct()
            .ToList() ?? [];

    public bool PasswordChangeRequired =>
        accessor.HttpContext?.User.HasClaim(AuthClaims.PasswordChangeRequired, "true") == true;
}
