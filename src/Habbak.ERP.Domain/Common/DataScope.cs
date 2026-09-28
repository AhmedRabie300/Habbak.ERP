namespace Habbak.ERP.Domain.Common;

/// <summary>
/// How wide a role's data reach is on a given screen, on top of the existing
/// Company/Branch scope (Settings-Permissions-BranchScope.md): a role's row for a screen names one
/// of these (Docs/Implementation/HR-Core-Plan.md §0.1). <see cref="Self"/> and <see cref="Team"/> are
/// declared now for the HR/self-service screens that will consume them later — nothing filters by
/// them yet, since no entity implements <see cref="IEmployeeScopedEntity"/> before the HR module's
/// Employee entity exists.
/// </summary>
public enum DataScope
{
    Company = 1,
    Branch = 2,
    Team = 3,
    Self = 4,
}
