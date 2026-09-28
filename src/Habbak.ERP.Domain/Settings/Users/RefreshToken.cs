using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Settings;

/// <summary>
/// One refresh token per sign-in session. <see cref="Token"/> holds a SHA-256 hash of the token the
/// client received, never the token itself, so a database leak cannot be replayed as a session.
/// </summary>
public class RefreshToken : AuditableEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public string Token { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }
    public string CreatedByIp { get; set; } = null!;
    public string? UserAgent { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }

    /// <summary>Hash of the token issued when this one was rotated.</summary>
    public string? ReplacedByToken { get; set; }

    /// <summary>Not stored — computed on read.</summary>
    public bool IsActive => RevokedAtUtc == null && DateTime.UtcNow < ExpiresAtUtc;
}
