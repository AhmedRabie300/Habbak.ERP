namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>Where the current request came from — for login attempts, sessions and the audit log. Null outside HTTP.</summary>
public interface IRequestInfo
{
    string? IpAddress { get; }
    string? UserAgent { get; }
}

/// <summary>The role codes in the current access token, and whether it may only be used to change the password.</summary>
public interface ICurrentUserRoles
{
    IReadOnlyList<string> RoleCodes { get; }
    bool PasswordChangeRequired { get; }
}

/// <summary>
/// The screen (MenuItem code) the current request is made from — the X-Screen-Code header the app
/// sends, when the user may open that screen, else the screen of the controller called. Null outside
/// a request. Field permissions are per screen, so the same endpoint can answer differently for the
/// customers screen and the invoice screen.
/// </summary>
public interface ICurrentScreen
{
    string? Code { get; }
}

/// <summary>
/// Encrypts secrets the server must read back (the authenticator secret) and issues short-lived
/// tamper-proof tokens (the two-factor sign-in challenge). Implemented with ASP.NET Data Protection
/// in the API layer, where its keys live.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    /// <summary>Null when the value was not produced by <see cref="Protect"/> with the current keys.</summary>
    string? Unprotect(string protectedValue);

    string ProtectFor(string plaintext, TimeSpan lifetime);

    /// <summary>Null when the token is forged, damaged or past its lifetime.</summary>
    string? UnprotectTimed(string token);
}

/// <summary>Signs access tokens (JWT, implemented in the API layer where the signing key lives).</summary>
public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(AccessTokenSubject subject, TimeSpan lifetime);
}

public sealed record AccessTokenSubject(
    long UserId, string Username, long CompanyId, long? BranchId, long? EmployeeId, IReadOnlyList<string> RoleCodes, bool PasswordChangeRequired);

public sealed record IssuedAccessToken(string Token, DateTime ExpiresAtUtc);
