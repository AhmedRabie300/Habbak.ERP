using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Settings.Auth;

/// <summary>What sign-in returns instead of a session when the user has two-factor sign-in.</summary>
public sealed record TwoFactorChallengeDto(bool TwoFactorRequired, string ChallengeToken, DateTime ExpiresAtUtc);

public sealed record MyTwoFactorDto(bool Enabled, int RecoveryCodesLeft);

/// <summary>The secret for manual entry and the otpauth:// link the QR code carries.</summary>
public sealed record TwoFactorSetupDto(string Secret, string SetupUri);

/// <summary>Shown once — only their hashes are kept.</summary>
public sealed record RecoveryCodesDto(IReadOnlyList<string> Codes);

/// <summary>
/// Two-factor sign-in: a code from an authenticator app (TOTP) after the password, or one of ten
/// single-use recovery codes. The secret is kept encrypted (ISecretProtector); the sign-in
/// challenge between the two steps is a short-lived token the server signed, so nothing is stored
/// for a half-finished sign-in.
/// </summary>
public static class TwoFactorRules
{
    public static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);
    public const int RecoveryCodeCount = 10;
    public const string Issuer = "Habbak ERP";

    private const string ChallengePurpose = "2fa-login";

    // No 0/O, 1/I/L — codes are read off paper.
    private const string RecoveryAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public const string InvalidCode = "كود التحقق غلط.";
    public const string ChallengeEnded = "انتهت مهلة كود التحقق — سجّل الدخول تاني.";

    public static TwoFactorChallengeDto IssueChallenge(ISecretProtector protector, User user, long? companyId, long? branchId, DateTime utcNow)
    {
        // The password-change time binds the challenge to the password that earned it.
        var payload = string.Join('|', ChallengePurpose, user.Id, companyId?.ToString(CultureInfo.InvariantCulture) ?? "",
            branchId?.ToString(CultureInfo.InvariantCulture) ?? "", (user.PasswordChangedAtUtc ?? user.CreatedAtUtc).Ticks);
        return new TwoFactorChallengeDto(true, protector.ProtectFor(payload, ChallengeLifetime), utcNow.Add(ChallengeLifetime));
    }

    public sealed record Challenge(long UserId, long? CompanyId, long? BranchId, long PasswordStamp);

    public static Challenge? ReadChallenge(ISecretProtector protector, string token)
    {
        var parts = protector.UnprotectTimed(token)?.Split('|');
        if (parts is not { Length: 5 } || parts[0] != ChallengePurpose || !long.TryParse(parts[1], out var userId) || !long.TryParse(parts[4], out var stamp))
        {
            return null;
        }

        return new Challenge(userId, long.TryParse(parts[2], out var c) ? c : null, long.TryParse(parts[3], out var b) ? b : null, stamp);
    }

    public static bool StampMatches(User user, Challenge challenge) => (user.PasswordChangedAtUtc ?? user.CreatedAtUtc).Ticks == challenge.PasswordStamp;

    /// <summary>Checks an authenticator code against the user's secret; a code is accepted once.</summary>
    public static bool CheckCode(ISecretProtector protector, User user, string? code, DateTime utcNow)
    {
        var secret = user.TwoFactorSecret is null ? null : protector.Unprotect(user.TwoFactorSecret);
        if (secret is null)
        {
            return false;
        }

        var step = Totp.Verify(secret, code, utcNow, user.TwoFactorLastStep);
        if (step is null)
        {
            return false;
        }

        user.TwoFactorLastStep = step;
        return true;
    }

    /// <summary>Uses up one recovery code; false when it is not one of the user's unused codes.</summary>
    public static async Task<bool> UseRecoveryCodeAsync(IApplicationDbContext db, User user, string? code, DateTime utcNow, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var hash = HashRecoveryCode(code);
        var row = await db.UserRecoveryCodes.FirstOrDefaultAsync(c => c.UserId == user.Id && c.CodeHash == hash && c.UsedAtUtc == null, ct);
        if (row is null)
        {
            return false;
        }

        row.UsedAtUtc = utcNow;
        return true;
    }

    /// <summary>Replaces the user's recovery codes with ten new ones and returns them (the only time they are readable).</summary>
    public static async Task<IReadOnlyList<string>> NewRecoveryCodesAsync(IApplicationDbContext db, User user, CancellationToken ct)
    {
        foreach (var old in await db.UserRecoveryCodes.Where(c => c.UserId == user.Id).ToListAsync(ct))
        {
            db.UserRecoveryCodes.Remove(old);
        }

        var codes = new List<string>(RecoveryCodeCount);
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var chars = RandomNumberGenerator.GetItems<char>(RecoveryAlphabet, 10);
            var code = $"{new string(chars, 0, 5)}-{new string(chars, 5, 5)}";
            codes.Add(code);
            db.UserRecoveryCodes.Add(new UserRecoveryCode { UserId = user.Id, CodeHash = HashRecoveryCode(code) });
        }

        return codes;
    }

    /// <summary>Two-factor sign-in off: secret, pending secret and recovery codes all go.</summary>
    public static async Task TurnOffAsync(IApplicationDbContext db, User user, CancellationToken ct)
    {
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorPendingSecret = null;
        user.TwoFactorLastStep = null;
        foreach (var code in await db.UserRecoveryCodes.Where(c => c.UserId == user.Id).ToListAsync(ct))
        {
            db.UserRecoveryCodes.Remove(code);
        }
    }

    /// <summary>Case, spaces and dashes do not matter when typing a recovery code.</summary>
    public static string HashRecoveryCode(string code)
    {
        var normalized = new string(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    public static async Task<User> CurrentUserAsync(IApplicationDbContext db, ICurrentCompanyContext current, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == current.UserId, ct)
        ?? throw new AuthenticationFailedException("AUTH-SESSION-ENDED", "انتهت الجلسة — سجّل الدخول تاني.");
}

// ------------------------------------------------------------------------------ sign-in, step two

/// <summary>Finishes a sign-in that asked for a second factor: an authenticator code or a recovery code.</summary>
public sealed record VerifyTwoFactorLoginCommand(string ChallengeToken, string? Code, string? RecoveryCode) : IRequest<AuthSessionDto>;

public sealed class VerifyTwoFactorLoginCommandValidator : AbstractValidator<VerifyTwoFactorLoginCommand>
{
    public VerifyTwoFactorLoginCommandValidator()
    {
        RuleFor(x => x.ChallengeToken).NotEmpty().MaximumLength(2000);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Code) || !string.IsNullOrWhiteSpace(x.RecoveryCode))
            .WithMessage("اكتب كود التحقق أو كود استرداد.");
        RuleFor(x => x.Code).MaximumLength(20);
        RuleFor(x => x.RecoveryCode).MaximumLength(40);
    }
}

public sealed class VerifyTwoFactorLoginCommandHandler(
    IApplicationDbContext db, SessionIssuer sessions, IRequestInfo requestInfo, ISecretProtector protector)
    : IRequestHandler<VerifyTwoFactorLoginCommand, AuthSessionDto>
{
    public async Task<AuthSessionDto> Handle(VerifyTwoFactorLoginCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var challenge = TwoFactorRules.ReadChallenge(protector, request.ChallengeToken)
                        ?? throw new AuthenticationFailedException("AUTH-2FA-CHALLENGE-ENDED", TwoFactorRules.ChallengeEnded);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == challenge.UserId, cancellationToken);
        if (user is null || !user.TwoFactorEnabled || !TwoFactorRules.StampMatches(user, challenge))
        {
            throw new AuthenticationFailedException("AUTH-2FA-CHALLENGE-ENDED", TwoFactorRules.ChallengeEnded);
        }

        var scopes = await sessions.ActiveScopesAsync(user.Id, cancellationToken);
        var scope = SessionIssuer.PickScope(scopes, challenge.CompanyId, challenge.BranchId);
        var settings = await sessions.SettingsForAsync(scope?.CompanyId, cancellationToken);

        user.ReleaseExpiredLock(now);
        if (user.Status == UserStatus.Locked)
        {
            await FailAsync(user, "Locked", now, cancellationToken);
            throw new AuthenticationFailedException("AUTH-USER-LOCKED", LoginCommandHandler.LockedMessage(user, now));
        }

        if (user.Status != UserStatus.Active || scope is null)
        {
            await FailAsync(user, scope is null ? "NoScope" : user.Status.ToString(), now, cancellationToken);
            throw new AuthenticationFailedException("AUTH-2FA-CHALLENGE-ENDED", TwoFactorRules.ChallengeEnded);
        }

        var passed = !string.IsNullOrWhiteSpace(request.Code)
            ? TwoFactorRules.CheckCode(protector, user, request.Code, now)
            : await TwoFactorRules.UseRecoveryCodeAsync(db, user, request.RecoveryCode, now, cancellationToken);

        if (!passed)
        {
            user.RegisterFailedLogin(settings.MaxFailedLoginAttempts, settings.AccountLockoutMinutes, now);
            await FailAsync(user, "WrongTwoFactorCode", now, cancellationToken);
            throw user.Status == UserStatus.Locked
                ? new AuthenticationFailedException("AUTH-USER-LOCKED", LoginCommandHandler.LockedMessage(user, now))
                : new AuthenticationFailedException("AUTH-2FA-INVALID-CODE", TwoFactorRules.InvalidCode);
        }

        user.RegisterSuccessfulLogin(now);
        var session = await sessions.IssueAsync(user, scope, scopes, settings, now, cancellationToken);

        var reason = string.IsNullOrWhiteSpace(request.Code) ? "RecoveryCode" : null;
        db.LoginAttempts.Add(LoginCommandHandler.Attempt(requestInfo, user.Id, user.Username, true, reason, now));
        db.AuditLogs.Add(sessions.AuditEvent(AuditActionType.Login, user.Id, scope.CompanyId, now,
            reason is null ? "{\"twoFactor\":\"code\"}" : "{\"twoFactor\":\"recoveryCode\"}"));
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    private async Task FailAsync(User user, string reason, DateTime now, CancellationToken ct)
    {
        db.LoginAttempts.Add(LoginCommandHandler.Attempt(requestInfo, user.Id, user.Username, false, reason, now));
        await db.SaveChangesAsync(ct);
    }
}

// ------------------------------------------------------------------------ the user's own settings

public sealed record GetMyTwoFactorQuery : IRequest<MyTwoFactorDto>;

public sealed class GetMyTwoFactorQueryHandler(IApplicationDbContext db, ICurrentCompanyContext current) : IRequestHandler<GetMyTwoFactorQuery, MyTwoFactorDto>
{
    public async Task<MyTwoFactorDto> Handle(GetMyTwoFactorQuery request, CancellationToken cancellationToken)
    {
        var user = await TwoFactorRules.CurrentUserAsync(db, current, cancellationToken);
        var left = await db.UserRecoveryCodes.CountAsync(c => c.UserId == user.Id && c.UsedAtUtc == null, cancellationToken);
        return new MyTwoFactorDto(user.TwoFactorEnabled, left);
    }
}

/// <summary>Step one of turning it on: a fresh secret, kept pending until a code from it comes back.</summary>
public sealed record BeginTwoFactorSetupCommand : IRequest<TwoFactorSetupDto>;

public sealed class BeginTwoFactorSetupCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, ISecretProtector protector)
    : IRequestHandler<BeginTwoFactorSetupCommand, TwoFactorSetupDto>
{
    public async Task<TwoFactorSetupDto> Handle(BeginTwoFactorSetupCommand request, CancellationToken cancellationToken)
    {
        var user = await TwoFactorRules.CurrentUserAsync(db, current, cancellationToken);
        if (user.TwoFactorEnabled)
        {
            throw new BusinessRuleException("AUTH-2FA-ALREADY-ON", "التحقق بخطوتين شغّال بالفعل — اقفله الأول لو عايز تربطه بموبايل تاني.");
        }

        var secret = Totp.NewSecret();
        user.TwoFactorPendingSecret = protector.Protect(secret);
        await db.SaveChangesAsync(cancellationToken);
        return new TwoFactorSetupDto(secret, Totp.SetupUri(TwoFactorRules.Issuer, user.Username, secret));
    }
}

/// <summary>Step two: a code from the app proves it holds the secret; the recovery codes are handed out.</summary>
public sealed record EnableTwoFactorCommand(string Code) : IRequest<RecoveryCodesDto>;

public sealed class EnableTwoFactorCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, ISecretProtector protector)
    : IRequestHandler<EnableTwoFactorCommand, RecoveryCodesDto>
{
    public async Task<RecoveryCodesDto> Handle(EnableTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var user = await TwoFactorRules.CurrentUserAsync(db, current, cancellationToken);
        if (user.TwoFactorEnabled)
        {
            throw new BusinessRuleException("AUTH-2FA-ALREADY-ON", "التحقق بخطوتين شغّال بالفعل.");
        }

        var secret = user.TwoFactorPendingSecret is null ? null : protector.Unprotect(user.TwoFactorPendingSecret);
        if (secret is null)
        {
            throw new BusinessRuleException("AUTH-2FA-NO-SETUP", "ابدأ الإعداد الأول عشان تاخد الكود السري.");
        }

        var step = Totp.Verify(secret, request.Code, DateTime.UtcNow, null)
                   ?? throw new BusinessRuleException("AUTH-2FA-INVALID-CODE", TwoFactorRules.InvalidCode);

        user.TwoFactorSecret = user.TwoFactorPendingSecret;
        user.TwoFactorPendingSecret = null;
        user.TwoFactorLastStep = step;
        user.TwoFactorEnabled = true;
        var codes = await TwoFactorRules.NewRecoveryCodesAsync(db, user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new RecoveryCodesDto(codes);
    }
}

/// <summary>Turning it off takes the password and a code (or a recovery code) — a stolen session alone cannot.</summary>
public sealed record DisableTwoFactorCommand(string Password, string? Code, string? RecoveryCode) : IRequest;

public sealed class DisableTwoFactorCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext current, ISecretProtector protector, IPasswordHasher hasher)
    : IRequestHandler<DisableTwoFactorCommand>
{
    public async Task Handle(DisableTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var user = await TwoFactorRules.CurrentUserAsync(db, current, cancellationToken);
        if (!user.TwoFactorEnabled)
        {
            return;
        }

        if (!hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new BusinessRuleException("AUTH-WRONG-CURRENT-PASSWORD", "كلمة المرور الحالية غلط.");
        }

        var passed = !string.IsNullOrWhiteSpace(request.Code)
            ? TwoFactorRules.CheckCode(protector, user, request.Code, now)
            : await TwoFactorRules.UseRecoveryCodeAsync(db, user, request.RecoveryCode, now, cancellationToken);
        if (!passed)
        {
            throw new BusinessRuleException("AUTH-2FA-INVALID-CODE", TwoFactorRules.InvalidCode);
        }

        await TwoFactorRules.TurnOffAsync(db, user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>New recovery codes (the old ones stop working) — takes a code from the app.</summary>
public sealed record RegenerateRecoveryCodesCommand(string Code) : IRequest<RecoveryCodesDto>;

public sealed class RegenerateRecoveryCodesCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, ISecretProtector protector)
    : IRequestHandler<RegenerateRecoveryCodesCommand, RecoveryCodesDto>
{
    public async Task<RecoveryCodesDto> Handle(RegenerateRecoveryCodesCommand request, CancellationToken cancellationToken)
    {
        var user = await TwoFactorRules.CurrentUserAsync(db, current, cancellationToken);
        if (!user.TwoFactorEnabled)
        {
            throw new BusinessRuleException("AUTH-2FA-OFF", "التحقق بخطوتين مش شغّال.");
        }

        if (!TwoFactorRules.CheckCode(protector, user, request.Code, DateTime.UtcNow))
        {
            throw new BusinessRuleException("AUTH-2FA-INVALID-CODE", TwoFactorRules.InvalidCode);
        }

        var codes = await TwoFactorRules.NewRecoveryCodesAsync(db, user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new RecoveryCodesDto(codes);
    }
}
