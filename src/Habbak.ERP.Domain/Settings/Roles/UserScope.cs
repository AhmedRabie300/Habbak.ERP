using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Settings;

/// <summary>
/// Which company (and optionally which branch) a user can work in. <see cref="RoleInScope"/> is a
/// role code, not a FK: roles belong to a company, and a scope names the role by its code.
/// </summary>
public class UserScope : AuditableEntity, ICompanyScopedEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public long? CompanyId { get; set; }

    /// <summary>Null = every branch of the company.</summary>
    public long? BranchId { get; set; }

    public string RoleInScope { get; set; } = null!;

    /// <summary>The scope the user lands in after signing in.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime GrantedAtUtc { get; set; }
    public long GrantedByUserId { get; set; }
}
