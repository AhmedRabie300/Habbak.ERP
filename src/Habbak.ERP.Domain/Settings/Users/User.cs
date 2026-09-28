using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Settings;

/// <summary>
/// A person who signs in. System-wide, not company-scoped: one login reaches several companies
/// through <see cref="UserScope"/> rows, so there is no CompanyId and no company query filter here.
/// Every user column in the other modules (CreatedBy, CashierUserId…) references this table since
/// phase 3 — see Persistence/Configurations/Settings/UserReferences.cs.
/// </summary>
public class User : AuditableEntity
{
    /// <summary>
    /// The system itself — startup seeding, background jobs, work done before anyone signs in. A real
    /// row (Id 0, suspended, no usable password) so every user column can carry a foreign key.
    /// </summary>
    public const long SystemUserId = 0;

    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;

    /// <summary>BCrypt hash. The salt is embedded in it, so <see cref="PasswordSalt"/> stays empty for BCrypt.</summary>
    public string PasswordHash { get; set; } = null!;
    public string PasswordSalt { get; set; } = string.Empty;

    public string FullName { get; set; } = null!;
    public PreferredLanguage PreferredLanguage { get; set; } = PreferredLanguage.Arabic;
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// [DEPRECATED — READ ONLY] Left over from before the HR module existed. The official link is now
    /// <see cref="Habbak.ERP.Domain.HR.Employee.UserId"/> (Docs/Modules/10-Module-HR-Payroll.md, rule 1) —
    /// this column is read-only during the transition (Phase 1.2 → Phase 2, HR-MASTER-PLAN.md §Phase
    /// 1.2) and nothing writes to it. Confirmed by code+data audit in Phase-1.2-Research.md: zero reads,
    /// zero writes, both existing rows NULL. Null for a system-only account.
    /// </summary>
    public long? EmployeeId { get; set; }

    public UserStatus Status { get; set; } = UserStatus.PendingActivation;
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public bool MustChangePassword { get; set; }

    /// <summary>When the password was last set — SystemSettings.PasswordExpiryDays counts from here (from CreatedAtUtc while null).</summary>
    public DateTime? PasswordChangedAtUtc { get; set; }

    public bool PasswordExpired(int expiryDays, DateTime utcNow) =>
        expiryDays > 0 && (PasswordChangedAtUtc ?? CreatedAtUtc).AddDays(expiryDays) <= utcNow;

    /// <summary>
    /// Sign-in asks for a code from an authenticator app (TOTP) after the password — or one of the
    /// user's recovery codes. Turned on by the user; an administrator can only turn it off (lost phone).
    /// </summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>The authenticator secret, encrypted (ISecretProtector). Set while TwoFactorEnabled.</summary>
    public string? TwoFactorSecret { get; set; }

    /// <summary>A secret handed out by setup but not yet confirmed with a code, encrypted.</summary>
    public string? TwoFactorPendingSecret { get; set; }

    /// <summary>The 30-second step of the last code accepted — a code is good once, never replayed.</summary>
    public long? TwoFactorLastStep { get; set; }

    public ICollection<UserRecoveryCode> RecoveryCodes { get; set; } = new List<UserRecoveryCode>();

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserScope> UserScopes { get; set; } = new List<UserScope>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    /// <summary>
    /// A wrong password. The attempt that reaches <paramref name="maxFailedAttempts"/> locks the
    /// account for <paramref name="lockoutMinutes"/> (SystemSettings.MaxFailedLoginAttempts /
    /// AccountLockoutMinutes).
    /// </summary>
    public void RegisterFailedLogin(int maxFailedAttempts, int lockoutMinutes, DateTime utcNow)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxFailedAttempts && Status == UserStatus.Active)
        {
            Status = UserStatus.Locked;
            LockedUntilUtc = utcNow.AddMinutes(lockoutMinutes);
        }
    }

    /// <summary>
    /// Ends an expired lockout. Meant to run before every login check, so a locked account opens
    /// by itself once the window passes — no scheduled job needed. Returns true when it did.
    /// </summary>
    public bool ReleaseExpiredLock(DateTime utcNow)
    {
        if (Status != UserStatus.Locked || LockedUntilUtc is null || utcNow < LockedUntilUtc)
        {
            return false;
        }

        Status = UserStatus.Active;
        LockedUntilUtc = null;
        FailedLoginAttempts = 0;
        return true;
    }

    public void RegisterSuccessfulLogin(DateTime utcNow)
    {
        FailedLoginAttempts = 0;
        LastLoginAtUtc = utcNow;
    }
}
