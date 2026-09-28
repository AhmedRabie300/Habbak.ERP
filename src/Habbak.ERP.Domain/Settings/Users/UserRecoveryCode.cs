using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Settings;

/// <summary>
/// A one-time code that stands in for the authenticator app (a lost or reset phone). Ten are issued
/// when two-factor sign-in is turned on; each works once. <see cref="CodeHash"/> is a SHA-256 hash —
/// the code itself is shown to the user once and never stored.
/// </summary>
public class UserRecoveryCode : AuditableEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public string CodeHash { get; set; } = null!;
    public DateTime? UsedAtUtc { get; set; }
}
