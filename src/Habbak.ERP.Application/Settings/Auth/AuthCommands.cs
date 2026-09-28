using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Settings.Auth;

// ------------------------------------------------------------------------------------------ login

public sealed record LoginCommand(string Username, string Password, long? CompanyId = null, long? BranchId = null) : IRequest<LoginResultDto>;

/// <summary>Either the session, or — for a user with two-factor sign-in — the challenge the code goes with.</summary>
public sealed record LoginResultDto(AuthSessionDto? Session, TwoFactorChallengeDto? TwoFactor);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

/// <summary>
/// Every attempt is recorded, known user or not. A wrong username and a wrong password get the
/// same message, and an unknown username still pays for a BCrypt check, so neither the reply nor
/// its timing tells an attacker which usernames exist.
/// </summary>
public sealed class LoginCommandHandler(
    IApplicationDbContext db, IPasswordHasher hasher, SessionIssuer sessions, IRequestInfo requestInfo, ISecretProtector protector)
    : IRequestHandler<LoginCommand, LoginResultDto>
{
    // A hash of a random string nobody knows — checked against for unknown usernames only, to spend the same time.
    private static string? _timingDummyHash;

    public const string InvalidCredentials = "اسم المستخدم أو كلمة المرور غير صحيحة.";

    public async Task<LoginResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var username = request.Username.Trim();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user is null)
        {
            hasher.Verify(request.Password, _timingDummyHash ??= hasher.Hash(Guid.NewGuid().ToString()));
            await FailAsync(null, username, "UnknownUser", cancellationToken);
            throw new AuthenticationFailedException("AUTH-INVALID-CREDENTIALS", InvalidCredentials);
        }

        var scopes = await sessions.ActiveScopesAsync(user.Id, cancellationToken);
        var scope = SessionIssuer.PickScope(scopes, request.CompanyId, request.BranchId);
        var settings = await sessions.SettingsForAsync(scope?.CompanyId, cancellationToken);

        user.ReleaseExpiredLock(now);

        if (user.Status == UserStatus.Locked)
        {
            await FailAsync(user.Id, username, "Locked", cancellationToken);
            throw new AuthenticationFailedException("AUTH-USER-LOCKED", LockedMessage(user, now));
        }

        if (user.Status != UserStatus.Active)
        {
            await FailAsync(user.Id, username, user.Status.ToString(), cancellationToken);
            throw new AuthenticationFailedException("AUTH-USER-INACTIVE", "الحساب ده مش مفعّل — كلّم مدير النظام.");
        }

        if (!hasher.Verify(request.Password, user.PasswordHash))
        {
            user.RegisterFailedLogin(settings.MaxFailedLoginAttempts, settings.AccountLockoutMinutes, now);
            await FailAsync(user.Id, username, "WrongPassword", cancellationToken);
            throw user.Status == UserStatus.Locked
                ? new AuthenticationFailedException("AUTH-USER-LOCKED", LockedMessage(user, now))
                : new AuthenticationFailedException("AUTH-INVALID-CREDENTIALS", InvalidCredentials);
        }

        if (scope is null)
        {
            await FailAsync(user.Id, username, "NoScope", cancellationToken);
            throw new AuthenticationFailedException(
                "AUTH-NO-SCOPE",
                request.CompanyId is null ? "المستخدم ده مالوش صلاحية دخول على أي شركة." : "المستخدم ده مالوش صلاحية دخول على الشركة دي.");
        }

        // The password was right; the code decides. Failed attempts are not cleared yet, so guessing
        // codes counts toward the lockout like guessing passwords.
        if (user.TwoFactorEnabled)
        {
            await db.SaveChangesAsync(cancellationToken);
            return new LoginResultDto(null, TwoFactorRules.IssueChallenge(protector, user, request.CompanyId, request.BranchId, now));
        }

        user.RegisterSuccessfulLogin(now);
        var session = await sessions.IssueAsync(user, scope, scopes, settings, now, cancellationToken);

        db.LoginAttempts.Add(Attempt(requestInfo, user.Id, username, true, null, now));
        db.AuditLogs.Add(sessions.AuditEvent(AuditActionType.Login, user.Id, scope.CompanyId, now));
        await db.SaveChangesAsync(cancellationToken);
        return new LoginResultDto(session, null);
    }

    internal static string LockedMessage(User user, DateTime now)
    {
        var minutes = user.LockedUntilUtc is null ? 0 : (int)Math.Ceiling((user.LockedUntilUtc.Value - now).TotalMinutes);
        return $"الحساب مقفول بسبب محاولات دخول غلط كتير — جرّب تاني بعد {Math.Max(1, minutes)} دقيقة.";
    }

    private async Task FailAsync(long? userId, string username, string reason, CancellationToken cancellationToken)
    {
        db.LoginAttempts.Add(Attempt(requestInfo, userId, username, false, reason, DateTime.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
    }

    internal static LoginAttempt Attempt(IRequestInfo requestInfo, long? userId, string username, bool success, string? reason, DateTime now) => new()
    {
        UserId = userId,
        Username = SessionIssuer.Truncate(username, 100)!,
        Success = success,
        IpAddress = requestInfo.IpAddress ?? "unknown",
        UserAgent = SessionIssuer.Truncate(requestInfo.UserAgent, 500),
        FailureReason = reason,
        AttemptedAtUtc = now
    };
}

// ---------------------------------------------------------------------------------------- refresh

/// <summary>Exchanges a refresh token for a new pair. Passing a company switches the session to it (if the user has a scope there).</summary>
public sealed record RefreshSessionCommand(string RefreshToken, long? CompanyId = null, long? BranchId = null) : IRequest<AuthSessionDto>;

/// <summary>
/// Rotation with reuse detection: every refresh retires the token it was given. If a retired
/// token comes back, someone else holds a copy — every session of that user is revoked.
/// </summary>
public sealed class RefreshSessionCommandHandler(IApplicationDbContext db, SessionIssuer sessions) : IRequestHandler<RefreshSessionCommand, AuthSessionDto>
{
    private const string SessionEnded = "انتهت الجلسة — سجّل الدخول تاني.";

    public async Task<AuthSessionDto> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var hash = SessionIssuer.HashToken(request.RefreshToken);
        var token = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.Token == hash, cancellationToken);

        if (token is null)
        {
            throw new AuthenticationFailedException("AUTH-SESSION-ENDED", SessionEnded);
        }

        if (token.RevokedAtUtc is not null)
        {
            if (token.ReplacedByToken is not null)
            {
                await RevokeAllAsync(token.UserId, "ReuseDetected", now, cancellationToken);
                db.AuditLogs.Add(sessions.AuditEvent(AuditActionType.Logout, token.UserId, await sessions.HomeCompanyAsync(token.UserId, cancellationToken), now, "{\"reason\":\"RefreshTokenReuse\"}"));
                await db.SaveChangesAsync(cancellationToken);
            }

            throw new AuthenticationFailedException("AUTH-SESSION-ENDED", SessionEnded);
        }

        if (token.ExpiresAtUtc <= now || token.User.Status != UserStatus.Active)
        {
            throw new AuthenticationFailedException("AUTH-SESSION-ENDED", SessionEnded);
        }

        var scopes = await sessions.ActiveScopesAsync(token.UserId, cancellationToken);
        var scope = SessionIssuer.PickScope(scopes, request.CompanyId, request.BranchId);
        if (scope is null || (request.CompanyId is not null && scope.CompanyId != request.CompanyId))
        {
            throw new ForbiddenException("AUTH-NO-SCOPE", "المستخدم ده مالوش صلاحية دخول على الشركة دي.");
        }

        var settings = await sessions.SettingsForAsync(scope.CompanyId, cancellationToken);
        var session = await sessions.IssueAsync(token.User, scope, scopes, settings, now, cancellationToken);

        token.RevokedAtUtc = now;
        token.RevokedReason = "Rotated";
        token.ReplacedByToken = SessionIssuer.HashToken(session.RefreshToken);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    private async Task RevokeAllAsync(long userId, string reason, DateTime now, CancellationToken cancellationToken)
    {
        var active = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var t in active)
        {
            t.RevokedAtUtc = now;
            t.RevokedReason = reason;
        }
    }
}

// ----------------------------------------------------------------------------------------- logout

public sealed record LogoutCommand(string RefreshToken) : IRequest;

public sealed class LogoutCommandHandler(IApplicationDbContext db, SessionIssuer sessions) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var hash = SessionIssuer.HashToken(request.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == hash && t.RevokedAtUtc == null, cancellationToken);
        if (token is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        token.RevokedAtUtc = now;
        token.RevokedReason = "Logout";
        db.AuditLogs.Add(sessions.AuditEvent(AuditActionType.Logout, token.UserId, await sessions.HomeCompanyAsync(token.UserId, cancellationToken), now));
        await db.SaveChangesAsync(cancellationToken);
    }
}

// -------------------------------------------------------------------------------- change password

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<AuthSessionDto>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MaximumLength(200);
    }
}

/// <summary>Checks the company's password policy, ends every other session, and hands back a fresh one without the change-required flag.</summary>
public sealed class ChangePasswordCommandHandler(
    IApplicationDbContext db, IPasswordHasher hasher, SessionIssuer sessions, ICurrentCompanyContext current)
    : IRequestHandler<ChangePasswordCommand, AuthSessionDto>
{
    public async Task<AuthSessionDto> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.UserId, cancellationToken)
                   ?? throw new AuthenticationFailedException("AUTH-SESSION-ENDED", "انتهت الجلسة — سجّل الدخول تاني.");

        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new BusinessRuleException("AUTH-WRONG-CURRENT-PASSWORD", "كلمة المرور الحالية غلط.");
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            throw new BusinessRuleException("AUTH-PASSWORD-UNCHANGED", "كلمة المرور الجديدة لازم تختلف عن الحالية.");
        }

        var settings = await sessions.SettingsForAsync(current.CompanyId, cancellationToken);
        PasswordRules.Ensure(settings, request.NewPassword);

        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.PasswordSalt = string.Empty;
        user.PasswordChangedAtUtc = now;
        user.MustChangePassword = false;

        foreach (var t in await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAtUtc == null).ToListAsync(cancellationToken))
        {
            t.RevokedAtUtc = now;
            t.RevokedReason = "PasswordChanged";
        }

        var scopes = await sessions.ActiveScopesAsync(user.Id, cancellationToken);
        var scope = SessionIssuer.PickScope(scopes, current.CompanyId, current.BranchId)
                    ?? throw new ForbiddenException("AUTH-NO-SCOPE", "المستخدم ده مالوش صلاحية دخول على الشركة دي.");

        // The password change itself reaches the audit log through the context's own capture (redacted).
        var session = await sessions.IssueAsync(user, scope, scopes, settings, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }
}

public static class PasswordRules
{
    public static void Ensure(SystemSettings settings, string password)
    {
        var problems = settings.CheckPassword(password);
        if (problems.Count > 0)
        {
            throw new Common.Exceptions.ValidationException(problems.Select(p => new FluentValidation.Results.ValidationFailure("newPassword", p)));
        }
    }
}

// ----------------------------------------------------------------------------------------- me

public sealed record MyAccessDto(
    long UserId,
    string Username,
    string FullName,
    PreferredLanguage PreferredLanguage,
    long CompanyId,
    long? BranchId,
    IReadOnlyList<string> RoleCodes,
    bool IsFullAccess,
    bool PasswordChangeRequired,
    IReadOnlyDictionary<string, ScreenRights> Screens,
    IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, MyFieldRightsDto>>> FieldPermissions,
    IReadOnlyDictionary<string, Dictionary<string, bool>> ButtonPermissions,
    IReadOnlyList<AuthScopeDto> Scopes);

/// <summary>Screen → entity → field; every catalog field, resolved for the user.</summary>
public sealed record MyFieldRightsDto(bool CanView, bool CanEdit);

public sealed record GetMyAccessQuery : IRequest<MyAccessDto>;

public sealed class GetMyAccessQueryHandler(
    IApplicationDbContext db, ICurrentCompanyContext current, ICurrentUserRoles roles, IUserAccessService access, SessionIssuer sessions)
    : IRequestHandler<GetMyAccessQuery, MyAccessDto>
{
    public async Task<MyAccessDto> Handle(GetMyAccessQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == current.UserId, cancellationToken)
                   ?? throw new AuthenticationFailedException("AUTH-SESSION-ENDED", "انتهت الجلسة — سجّل الدخول تاني.");

        var rights = await access.GetCurrentAsync(cancellationToken);
        var scopes = await sessions.ActiveScopesAsync(user.Id, cancellationToken);

        return new MyAccessDto(
            user.Id, user.Username, user.FullName, user.PreferredLanguage, current.CompanyId, current.BranchId,
            rights.RoleCodes, rights.IsFullAccess, roles.PasswordChangeRequired,
            rights.Screens,
            rights.CatalogFields
                .GroupBy(f => f.Screen)
                .ToDictionary(
                    s => s.Key,
                    s => s.GroupBy(f => f.Entity).ToDictionary(e => e.Key, e => e.ToDictionary(f => f.Field, f => new MyFieldRightsDto(f.Rights.View, f.Rights.Edit)))),
            rights.CatalogButtons
                .GroupBy(b => b.Screen)
                .ToDictionary(s => s.Key, s => s.ToDictionary(b => b.Button, b => b.Allowed)),
            await sessions.DescribeScopesAsync(scopes, cancellationToken));
    }
}
