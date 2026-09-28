using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Settings;

/// <summary>A user holding a role — for the whole company, or for one branch when <see cref="BranchId"/> is set.</summary>
public class UserRole : AuditableEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public long? BranchId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public long AssignedByUserId { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
