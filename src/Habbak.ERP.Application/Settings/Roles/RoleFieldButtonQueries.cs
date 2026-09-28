using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Settings.Roles;

/// <summary>
/// A catalog field as a role has it on a screen. <see cref="IsConfigured"/> false: the role has no row,
/// so the field is open (viewable, editable, audited).
/// </summary>
public sealed record RoleFieldRightDto(
    string ScreenCode, string EntityType, string FieldName, bool CanView, bool CanEdit, bool RequiresAuditLog, bool IsConfigured);

/// <summary>
/// A catalog button as a role has it on a screen. <see cref="IsConfigured"/> false: no row, so
/// <see cref="IsEnabled"/> is what the role's screen permission gives (the button's FallbackAction).
/// </summary>
public sealed record RoleButtonRightDto(
    string ScreenCode, string ButtonCode, string NameAr, string NameEn, ScreenAction FallbackAction, bool ServerEnforced,
    bool IsEnabled, bool RequiresAuditLog, bool IsConfigured);

/// <summary>Every catalog field of the screen (or of every screen) with the role's rights on it.</summary>
public sealed record GetRoleFieldRightsQuery(long RoleId, string? ScreenCode) : IRequest<IReadOnlyList<RoleFieldRightDto>>;

public sealed class GetRoleFieldRightsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetRoleFieldRightsQuery, IReadOnlyList<RoleFieldRightDto>>
{
    public async Task<IReadOnlyList<RoleFieldRightDto>> Handle(GetRoleFieldRightsQuery request, CancellationToken cancellationToken)
    {
        await RoleFieldButtonRules.EnsureRoleAsync(db, request.RoleId, cancellationToken);
        var rows = await db.FieldPermissions.AsNoTracking()
            .Where(p => p.RoleId == request.RoleId && (request.ScreenCode == null || p.ScreenCode == request.ScreenCode))
            .ToListAsync(cancellationToken);

        return FieldPermissionCatalog.SensitiveFields
            .Where(s => request.ScreenCode == null || s.Key == request.ScreenCode)
            .SelectMany(s => s.Value.SelectMany(e => e.Value.Select(f => (Screen: s.Key, Entity: e.Key, Field: f))))
            .Select(x =>
            {
                var row = rows.FirstOrDefault(r => r.ScreenCode == x.Screen && r.EntityType == x.Entity && r.FieldName == x.Field);
                return row is null
                    ? new RoleFieldRightDto(x.Screen, x.Entity, x.Field, true, true, true, false)
                    : new RoleFieldRightDto(x.Screen, x.Entity, x.Field, row.CanView, row.CanEdit, row.RequiresAuditLog, true);
            })
            .ToList();
    }
}

/// <summary>Every catalog button of the screen (or of every screen) with the role's rights on it.</summary>
public sealed record GetRoleButtonRightsQuery(long RoleId, string? ScreenCode) : IRequest<IReadOnlyList<RoleButtonRightDto>>;

public sealed class GetRoleButtonRightsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetRoleButtonRightsQuery, IReadOnlyList<RoleButtonRightDto>>
{
    public async Task<IReadOnlyList<RoleButtonRightDto>> Handle(GetRoleButtonRightsQuery request, CancellationToken cancellationToken)
    {
        var role = await RoleFieldButtonRules.EnsureRoleAsync(db, request.RoleId, cancellationToken);
        var fullAccess = UserAccess.FullAccessRoles.Contains(role.Code);
        var rows = await db.ButtonPermissions.AsNoTracking()
            .Where(p => p.RoleId == request.RoleId && (request.ScreenCode == null || p.ScreenCode == request.ScreenCode))
            .ToListAsync(cancellationToken);
        var screens = await db.ScreenPermissions.AsNoTracking().Where(p => p.RoleId == request.RoleId).ToListAsync(cancellationToken);

        ScreenRights RightsOn(string screen) => screens.Where(p => p.ScreenCode == screen)
            .Select(p => new ScreenRights(p.CanView, p.CanAdd, p.CanEdit, p.CanDelete, p.CanPrint, p.CanExport, p.CanApprove))
            .Aggregate(ScreenRights.None, (a, b) => a.Or(b));

        return ButtonPermissionCatalog.Buttons
            .Where(s => request.ScreenCode == null || s.Key == request.ScreenCode)
            .SelectMany(s => s.Value.Select(b => (Screen: s.Key, Button: b)))
            .Select(x =>
            {
                var row = rows.FirstOrDefault(r => r.ScreenCode == x.Screen && r.ButtonCode == x.Button.Code);
                var enabled = fullAccess
                              || (row is null ? RightsOn(x.Screen).Allows(x.Button.FallbackAction) : row.IsEnabled && RightsOn(x.Screen).View);
                return new RoleButtonRightDto(
                    x.Screen, x.Button.Code, x.Button.NameAr, x.Button.NameEn, x.Button.FallbackAction, x.Button.ServerEnforced,
                    enabled, row?.RequiresAuditLog ?? true, row is not null);
            })
            .ToList();
    }
}

internal static class RoleFieldButtonRules
{
    public static async Task<Role> EnsureRoleAsync(IApplicationDbContext db, long roleId, CancellationToken ct) =>
        await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId, ct) ?? throw new NotFoundException(nameof(Role), roleId);
}
