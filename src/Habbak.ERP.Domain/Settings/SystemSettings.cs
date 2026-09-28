using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Settings;

/// <summary>Security policy for one company — one row each, created with the company.</summary>
public class SystemSettings : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    // Password policy
    public int PasswordMinLength { get; set; } = 8;
    public bool PasswordRequireUppercase { get; set; } = true;
    public bool PasswordRequireLowercase { get; set; } = true;
    public bool PasswordRequireDigit { get; set; } = true;
    public bool PasswordRequireSpecial { get; set; } = true;
    public int PasswordExpiryDays { get; set; } = 90;

    // Login policy
    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int AccountLockoutMinutes { get; set; } = 15;
    public int SessionTimeoutMinutes { get; set; } = 30;
    public int RefreshTokenExpiryDays { get; set; } = 7;

    // Audit
    public int AuditRetentionYears { get; set; } = 7;

    /// <summary>The rules <paramref name="password"/> breaks, in Arabic — empty when it is acceptable.</summary>
    public IReadOnlyList<string> CheckPassword(string password)
    {
        var problems = new List<string>();
        if (password.Length < PasswordMinLength) problems.Add($"كلمة المرور لازم تكون {PasswordMinLength} حروف على الأقل.");
        if (PasswordRequireUppercase && !password.Any(char.IsUpper)) problems.Add("كلمة المرور لازم فيها حرف كبير.");
        if (PasswordRequireLowercase && !password.Any(char.IsLower)) problems.Add("كلمة المرور لازم فيها حرف صغير.");
        if (PasswordRequireDigit && !password.Any(char.IsDigit)) problems.Add("كلمة المرور لازم فيها رقم.");
        if (PasswordRequireSpecial && password.All(char.IsLetterOrDigit)) problems.Add("كلمة المرور لازم فيها رمز (زي @ أو #).");
        return problems;
    }
}
