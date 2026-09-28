using System.Security.Cryptography;
using System.Text;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Settings.Auth;

/// <summary>One place the user can work: a company, and a branch when the scope is limited to one (its data is then limited to that branch).</summary>
public sealed record AuthScopeDto(
    long CompanyId, string CompanyNameAr, string CompanyNameEn, long? BranchId, string? BranchNameAr, string? BranchNameEn, string RoleInScope, bool IsDefault);

public sealed record AuthSessionDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    long UserId,
    string Username,
    string FullName,
    PreferredLanguage PreferredLanguage,
    long CompanyId,
    long? BranchId,
    IReadOnlyList<string> RoleCodes,
    bool PasswordChangeRequired,
    IReadOnlyList<AuthScopeDto> Scopes);

/// <summary>
/// Everything a new session needs: which company/branch it opens in, the user's roles there, and
/// a fresh access + refresh token pair. Runs before the user is authenticated (login, refresh),
/// so every company-scoped read ignores the query filters and filters by hand.
/// </summary>
public sealed class SessionIssuer(IApplicationDbContext db, IAccessTokenIssuer tokenIssuer, IRequestInfo requestInfo)
{
    public static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    public async Task<List<UserScope>> ActiveScopesAsync(long userId, CancellationToken cancellationToken) =>
        await db.UserScopes.IgnoreQueryFilters()
            .Where(s => s.UserId == userId && !s.IsDeleted && s.IsActive
                        && db.Companies.IgnoreQueryFilters().Any(c => c.Id == s.CompanyId && !c.IsDeleted && c.IsActive))
            .OrderByDescending(s => s.IsDefault).ThenBy(s => s.CompanyId).ThenBy(s => s.BranchId)
            .ToListAsync(cancellationToken);

    /// <summary>The requested company (and branch), else the default scope, else the first one. Null when the user may enter no company.</summary>
    public static UserScope? PickScope(IReadOnlyList<UserScope> scopes, long? companyId, long? branchId)
    {
        if (companyId is null)
        {
            return scopes.FirstOrDefault();
        }

        var inCompany = scopes.Where(s => s.CompanyId == companyId).ToList();
        return branchId is null
            ? inCompany.FirstOrDefault(s => s.BranchId == null) ?? inCompany.FirstOrDefault()
            : inCompany.FirstOrDefault(s => s.BranchId == branchId) ?? inCompany.FirstOrDefault(s => s.BranchId == null);
    }

    public async Task<SystemSettings> SettingsForAsync(long? companyId, CancellationToken cancellationToken) =>
        companyId is null
            ? new SystemSettings()
            : await db.SystemSettingsRows.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.CompanyId == companyId && !s.IsDeleted, cancellationToken)
              ?? new SystemSettings();

    public async Task<List<string>> RoleCodesAsync(long userId, UserScope scope, DateTime utcNow, CancellationToken cancellationToken)
    {
        var codes = await db.UserRoles.IgnoreQueryFilters()
            .Where(ur => ur.UserId == userId && !ur.IsDeleted
                         && (ur.ExpiresAtUtc == null || ur.ExpiresAtUtc > utcNow)
                         && (ur.BranchId == null || ur.BranchId == scope.BranchId)
                         && ur.Role.CompanyId == scope.CompanyId && !ur.Role.IsDeleted && ur.Role.IsActive)
            .Select(ur => ur.Role.Code)
            .ToListAsync(cancellationToken);

        codes.Add(scope.RoleInScope);
        return codes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Adds the refresh token row (not saved) and returns the session the client keeps.</summary>
    public async Task<AuthSessionDto> IssueAsync(
        User user, UserScope scope, IReadOnlyList<UserScope> allScopes, SystemSettings settings, DateTime utcNow, CancellationToken cancellationToken)
    {
        var roles = await RoleCodesAsync(user.Id, scope, utcNow, cancellationToken);
        var passwordChangeRequired = user.MustChangePassword || user.PasswordExpired(settings.PasswordExpiryDays, utcNow);

        var access = tokenIssuer.Issue(
            // EmployeeId is always null in Phase 0: the HR module's Employee.UserId link does not
            // exist yet (Docs/Implementation/HR-Core-Plan.md §0.1). Phase 1 resolves it here from
            // Employee.UserId once that entity exists.
            new AccessTokenSubject(user.Id, user.Username, scope.CompanyId!.Value, scope.BranchId, null, roles, passwordChangeRequired),
            TimeSpan.FromMinutes(Math.Max(1, settings.SessionTimeoutMinutes)));

        var rawRefresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var refreshExpires = utcNow.AddDays(Math.Max(1, settings.RefreshTokenExpiryDays));
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = HashToken(rawRefresh),
            ExpiresAtUtc = refreshExpires,
            CreatedByIp = requestInfo.IpAddress ?? "unknown",
            UserAgent = Truncate(requestInfo.UserAgent, 500)
        });

        return new AuthSessionDto(
            access.Token, access.ExpiresAtUtc, rawRefresh, refreshExpires,
            user.Id, user.Username, user.FullName, user.PreferredLanguage,
            scope.CompanyId.Value, scope.BranchId, roles, passwordChangeRequired,
            await DescribeScopesAsync(allScopes, cancellationToken));
    }

    /// <summary>The scopes with company and branch names, read past the query filters (the session may not be set up yet).</summary>
    public async Task<List<AuthScopeDto>> DescribeScopesAsync(IReadOnlyList<UserScope> scopes, CancellationToken cancellationToken)
    {
        var companyIds = scopes.Select(s => s.CompanyId).Distinct().ToList();
        var branchIds = scopes.Where(s => s.BranchId != null).Select(s => s.BranchId).Distinct().ToList();
        var companies = await db.Companies.IgnoreQueryFilters().Where(c => companyIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
        var branches = await db.Branches.IgnoreQueryFilters().Where(b => branchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, cancellationToken);

        return scopes.Select(s =>
        {
            var company = companies.GetValueOrDefault(s.CompanyId!.Value);
            var branch = s.BranchId is { } id ? branches.GetValueOrDefault(id) : null;
            return new AuthScopeDto(
                s.CompanyId.Value, company?.NameAr ?? "", company?.NameEn ?? "",
                s.BranchId, branch?.NameAr, branch?.NameEn, s.RoleInScope, s.IsDefault);
        }).ToList();
    }

    /// <summary>The company a sign-out is logged under — the refresh token does not record one, so the user's default scope.</summary>
    public async Task<long?> HomeCompanyAsync(long userId, CancellationToken cancellationToken) =>
        (await ActiveScopesAsync(userId, cancellationToken)).FirstOrDefault()?.CompanyId;

    public AuditLog AuditEvent(AuditActionType action, long userId, long? companyId, DateTime utcNow, string? details = null) => new()
    {
        CompanyId = companyId,
        UserId = userId,
        ActionType = action,
        EntityType = nameof(User),
        EntityId = userId,
        IpAddress = requestInfo.IpAddress,
        UserAgent = Truncate(requestInfo.UserAgent, 500),
        OccurredAtUtc = utcNow,
        AdditionalData = details
    };

    public static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
