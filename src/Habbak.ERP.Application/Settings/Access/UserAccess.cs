using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Habbak.ERP.Application.Settings.Access;

public enum ScreenAction
{
    View = 1,
    Add = 2,
    Edit = 3,
    Delete = 4,
    Print = 5,
    Export = 6,
    Approve = 7
}

public sealed record ScreenRights(bool View, bool Add, bool Edit, bool Delete, bool Print, bool Export, bool Approve)
{
    public static readonly ScreenRights None = new(false, false, false, false, false, false, false);
    public static readonly ScreenRights Full = new(true, true, true, true, true, true, true);

    public bool Allows(ScreenAction action) => action switch
    {
        ScreenAction.View => View,
        ScreenAction.Add => Add,
        ScreenAction.Edit => Edit,
        ScreenAction.Delete => Delete,
        ScreenAction.Print => Print,
        ScreenAction.Export => Export,
        ScreenAction.Approve => Approve,
        _ => false
    };

    public ScreenRights Or(ScreenRights other) => new(
        View || other.View, Add || other.Add, Edit || other.Edit, Delete || other.Delete,
        Print || other.Print, Export || other.Export, Approve || other.Approve);
}

public sealed record FieldRights(bool View, bool Edit, bool Audit);

/// <summary>
/// What the signed-in user may do in the current company: the union of their roles' permissions.
/// SUPER_ADMIN and COMPANY_ADMIN are full-access roles and skip every check.
/// Field rights are per screen and restrict only where a FieldPermission row exists: a sensitive
/// field nobody has configured stays visible and editable, so switching the module on hides nothing
/// by surprise. Buttons follow ButtonPermission rows, else the screen permission they fall back on.
/// </summary>
public sealed class UserAccess(
    bool isFullAccess,
    IReadOnlyList<string> roleCodes,
    IReadOnlyDictionary<string, ScreenRights> screens,
    IReadOnlyDictionary<(string Screen, string Entity, string Field), FieldRights> fields,
    IReadOnlyDictionary<(string Screen, string Button), ButtonRule> buttons)
{
    public static readonly IReadOnlyList<string> FullAccessRoles = [SystemRoles.SuperAdmin, SystemRoles.CompanyAdmin];

    private static readonly FieldRights Open = new(true, true, true);

    public bool IsFullAccess { get; } = isFullAccess;
    public IReadOnlyList<string> RoleCodes { get; } = roleCodes;
    public IReadOnlyDictionary<string, ScreenRights> Screens { get; } = screens;

    public ScreenRights For(string screenCode) =>
        IsFullAccess ? ScreenRights.Full : Screens.GetValueOrDefault(screenCode, ScreenRights.None);

    public bool Can(ScreenAction action, IEnumerable<string> screenCodes) =>
        IsFullAccess || screenCodes.Any(code => For(code).Allows(action));

    /// <summary>
    /// A sensitive field on a screen. No row on that screen = no restriction. No screen (work done
    /// outside a request) = no restriction either: rules are about what a person has in front of them.
    /// </summary>
    public FieldRights FieldOn(string? screenCode, string entityType, string fieldName) =>
        IsFullAccess || screenCode is null
            ? Open
            : fields.GetValueOrDefault((screenCode, entityType, fieldName), Open);

    /// <summary>Every catalog field, resolved for this user — what /auth/me hands the screens.</summary>
    public IEnumerable<(string Screen, string Entity, string Field, FieldRights Rights)> CatalogFields =>
        FieldPermissionCatalog.SensitiveFields.SelectMany(screen => screen.Value.SelectMany(entity =>
            entity.Value.Select(name => (screen.Key, entity.Key, name, FieldOn(screen.Key, entity.Key, name)))));

    /// <summary>The role rows for a button, if any role has one.</summary>
    public ButtonRule? ButtonRow(string screenCode, string buttonCode) =>
        buttons.TryGetValue((screenCode, buttonCode), out var rule) ? rule : null;

    /// <summary>
    /// Whether the button may be pressed: a row decides (and still needs the screen open to the
    /// user); no row falls back to the screen permission the catalog names for it.
    /// </summary>
    public bool ButtonAllowed(string screenCode, string buttonCode)
    {
        if (IsFullAccess)
        {
            return true;
        }

        if (ButtonRow(screenCode, buttonCode) is { } row)
        {
            return row.Enabled && For(screenCode).View;
        }

        var definition = ButtonPermissionCatalog.Find(screenCode, buttonCode);
        return definition is not null && For(screenCode).Allows(definition.FallbackAction);
    }

    public bool ButtonAudited(string screenCode, string buttonCode) => ButtonRow(screenCode, buttonCode)?.Audit ?? true;

    /// <summary>Every catalog button, resolved for this user.</summary>
    public IEnumerable<(string Screen, string Button, bool Allowed)> CatalogButtons =>
        ButtonPermissionCatalog.Buttons.SelectMany(screen => screen.Value.Select(b => (screen.Key, b.Code, ButtonAllowed(screen.Key, b.Code))));
}

public sealed record ButtonRule(bool Enabled, bool Audit);

public interface IUserAccessService
{
    /// <summary>The current request's user, in the current company.</summary>
    Task<UserAccess> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<UserAccess> BuildAsync(long companyId, IReadOnlyList<string> roleCodes, CancellationToken cancellationToken = default);

    /// <summary>Call after any change to roles or permissions; cached answers are dropped.</summary>
    void Invalidate();

    /// <summary>The current user, on a given screen (the request's screen when null).</summary>
    Task<bool> HasFieldPermissionOnScreenAsync(string? screenCode, string entityType, string fieldName, FieldAction action, CancellationToken cancellationToken = default);

    Task<bool> HasButtonPermissionAsync(string screenCode, string buttonCode, CancellationToken cancellationToken = default);

    Task<bool> ShouldAuditButtonAsync(string screenCode, string buttonCode, CancellationToken cancellationToken = default);
}

public enum FieldAction
{
    View = 1,
    Edit = 2
}

/// <summary>
/// Cached per (company, role set) for a minute and dropped on every permission change. Roles come
/// from the access token, so a change to a user's roles applies at their next token refresh
/// (the session timeout, 30 minutes by default); a change to a role's permissions applies at once.
/// </summary>
public sealed class UserAccessService(
    IApplicationDbContext db, ICurrentCompanyContext currentCompany, ICurrentUserRoles currentRoles, ICurrentScreen currentScreen, IMemoryCache cache)
    : IUserAccessService
{
    private static int _version;

    public Task<UserAccess> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        BuildAsync(currentCompany.CompanyId, currentRoles.RoleCodes, cancellationToken);

    public async Task<UserAccess> BuildAsync(long companyId, IReadOnlyList<string> roleCodes, CancellationToken cancellationToken = default)
    {
        var codes = roleCodes.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (codes.Any(c => UserAccess.FullAccessRoles.Contains(c, StringComparer.OrdinalIgnoreCase)))
        {
            return new UserAccess(true, codes, new Dictionary<string, ScreenRights>(), new Dictionary<(string, string, string), FieldRights>(), new Dictionary<(string, string), ButtonRule>());
        }

        var key = $"access:{Volatile.Read(ref _version)}:{companyId}:{string.Join(',', codes)}";
        if (cache.TryGetValue(key, out UserAccess? cached) && cached is not null)
        {
            return cached;
        }

        var roleIds = await db.Roles.IgnoreQueryFilters()
            .Where(r => r.CompanyId == companyId && !r.IsDeleted && r.IsActive && codes.Contains(r.Code))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var screenRows = await db.ScreenPermissions.IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && roleIds.Contains(p.RoleId))
            .ToListAsync(cancellationToken);

        var screens = screenRows
            .GroupBy(p => p.ScreenCode)
            .ToDictionary(
                g => g.Key,
                g => g.Aggregate(ScreenRights.None, (acc, p) =>
                    acc.Or(new ScreenRights(p.CanView, p.CanAdd, p.CanEdit, p.CanDelete, p.CanPrint, p.CanExport, p.CanApprove))),
                StringComparer.OrdinalIgnoreCase);

        var fieldRows = await db.FieldPermissions.IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && roleIds.Contains(p.RoleId))
            .ToListAsync(cancellationToken);

        var fields = fieldRows
            .GroupBy(p => (p.ScreenCode, p.EntityType, p.FieldName))
            .ToDictionary(
                g => g.Key,
                g => new FieldRights(g.Any(p => p.CanView), g.Any(p => p.CanEdit), g.Any(p => p.RequiresAuditLog)));

        // Several roles: the button is on if any of them turns it on.
        var buttons = (await db.ButtonPermissions.IgnoreQueryFilters()
                .Where(p => !p.IsDeleted && roleIds.Contains(p.RoleId))
                .ToListAsync(cancellationToken))
            .GroupBy(p => (p.ScreenCode, p.ButtonCode))
            .ToDictionary(g => g.Key, g => new ButtonRule(g.Any(p => p.IsEnabled), g.Any(p => p.RequiresAuditLog)));

        var access = new UserAccess(false, codes, screens, fields, buttons);
        cache.Set(key, access, TimeSpan.FromMinutes(1));
        return access;
    }

    public void Invalidate() => Interlocked.Increment(ref _version);

    public async Task<bool> HasFieldPermissionOnScreenAsync(
        string? screenCode, string entityType, string fieldName, FieldAction action, CancellationToken cancellationToken = default)
    {
        var rights = (await GetCurrentAsync(cancellationToken)).FieldOn(screenCode ?? currentScreen.Code, entityType, fieldName);
        return action == FieldAction.View ? rights.View : rights.Edit;
    }

    public async Task<bool> HasButtonPermissionAsync(string screenCode, string buttonCode, CancellationToken cancellationToken = default) =>
        (await GetCurrentAsync(cancellationToken)).ButtonAllowed(screenCode, buttonCode);

    public async Task<bool> ShouldAuditButtonAsync(string screenCode, string buttonCode, CancellationToken cancellationToken = default) =>
        (await GetCurrentAsync(cancellationToken)).ButtonAudited(screenCode, buttonCode);
}
