using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Settings;

/// <summary>
/// What every company starts with: the eight system roles, a SystemSettings row with the default
/// policy, and access for every super admin. Called by CreateCompanyCommand in the same save as
/// the company. Companies that existed before this module got the same rows from the
/// AddSettingsPermissionsPhase1 migration.
/// </summary>
public static class CompanySecurityDefaults
{
    /// <summary>The user the AddSettingsPermissionsPhase1 migration seeds.</summary>
    public const string BootstrapAdminUsername = "admin";

    /// <summary>Adds (does not save) the defaults for an already-saved company.</summary>
    public static async Task AddAsync(IApplicationDbContext db, long companyId, DateTime utcNow, long grantedByUserId, CancellationToken cancellationToken)
    {
        var roles = SystemRoles.All
            .Select(r => new Role { CompanyId = companyId, Code = r.Code, NameAr = r.NameAr, NameEn = r.NameEn, IsSystemRole = true, IsActive = true })
            .ToList();
        db.Roles.AddRange(roles);
        db.SystemSettingsRows.Add(new SystemSettings { CompanyId = companyId });

        // A super admin reaches every company, including ones created after them. Roles are
        // company-scoped, so this looks across companies.
        var superAdminIds = await db.UserRoles
            .IgnoreQueryFilters()
            .Where(ur => !ur.IsDeleted && ur.Role.Code == SystemRoles.SuperAdmin && !ur.Role.IsDeleted && !ur.User.IsDeleted)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // A fresh installation has no super admin yet (the migration found no company to make the
        // seeded "admin" super admin of) — the first company makes it one, or nobody could sign in.
        if (superAdminIds.Count == 0)
        {
            var bootstrapAdmin = await db.Users.Where(u => u.Username == BootstrapAdminUsername).Select(u => (long?)u.Id).FirstOrDefaultAsync(cancellationToken);
            if (bootstrapAdmin is not null)
            {
                superAdminIds.Add(bootstrapAdmin.Value);
            }
        }

        var superAdminRole = roles.Single(r => r.Code == SystemRoles.SuperAdmin);
        foreach (var userId in superAdminIds)
        {
            db.UserRoles.Add(new UserRole { UserId = userId, Role = superAdminRole, AssignedAtUtc = utcNow, AssignedByUserId = grantedByUserId });
            db.UserScopes.Add(new UserScope
            {
                UserId = userId, CompanyId = companyId, RoleInScope = SystemRoles.SuperAdmin,
                IsDefault = false, IsActive = true, GrantedAtUtc = utcNow, GrantedByUserId = grantedByUserId
            });
        }
    }
}
