using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Settings;

/// <summary>Every sign-in attempt, successful or not — including usernames that do not exist (UserId null).</summary>
public class LoginAttempt : AuditableEntity
{
    public long? UserId { get; set; }
    public string Username { get; set; } = null!;
    public bool Success { get; set; }
    public string IpAddress { get; set; } = null!;
    public string? UserAgent { get; set; }
    public string? FailureReason { get; set; }
    public DateTime AttemptedAtUtc { get; set; }
}
