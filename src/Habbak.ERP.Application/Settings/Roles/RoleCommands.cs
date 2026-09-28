using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Settings.Roles;

public sealed record RoleListItemDto(
    long Id, string Code, string NameAr, string NameEn, string? Description, bool IsSystemRole, bool IsActive,
    bool IsFullAccess, int UserCount, int ScreenCount);

public sealed record ScreenPermissionDto(string ScreenCode, bool CanView, bool CanAdd, bool CanEdit, bool CanDelete, bool CanPrint, bool CanExport, bool CanApprove);

public sealed record FieldPermissionDto(string ScreenCode, string EntityType, string FieldName, bool CanView, bool CanEdit, bool RequiresAuditLog);

public sealed record ButtonPermissionDto(string ScreenCode, string ButtonCode, bool IsEnabled, bool RequiresAuditLog);

public sealed record RoleDetailDto(
    long Id, string Code, string NameAr, string NameEn, string? Description, bool IsSystemRole, bool IsActive, bool IsFullAccess,
    IReadOnlyList<ScreenPermissionDto> Screens, IReadOnlyList<FieldPermissionDto> Fields, IReadOnlyList<ButtonPermissionDto> Buttons,
    string RowVersion);

public sealed record PermissionScreenDto(string Code, string NameAr, string NameEn, string GroupNameAr, string GroupNameEn, string RouteKey);

public sealed record PermissionFieldDto(string EntityType, string FieldName);

/// <summary>A screen that has sensitive fields, and those fields.</summary>
public sealed record FieldCatalogScreenDto(string ScreenCode, string NameAr, string NameEn, IReadOnlyList<PermissionFieldDto> Fields);

public sealed record PermissionButtonDto(string Code, string NameAr, string NameEn, ScreenAction FallbackAction, bool ServerEnforced);

/// <summary>A screen that has special buttons, and those buttons.</summary>
public sealed record ButtonCatalogScreenDto(string ScreenCode, string NameAr, string NameEn, IReadOnlyList<PermissionButtonDto> Buttons);

public sealed record PermissionCatalogDto(
    IReadOnlyList<PermissionScreenDto> Screens, IReadOnlyList<FieldCatalogScreenDto> FieldScreens, IReadOnlyList<ButtonCatalogScreenDto> ButtonScreens);

// ------------------------------------------------------------------------------------ queries

public sealed record GetRolesListQuery : IRequest<IReadOnlyList<RoleListItemDto>>;

public sealed class GetRolesListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetRolesListQuery, IReadOnlyList<RoleListItemDto>>
{
    public async Task<IReadOnlyList<RoleListItemDto>> Handle(GetRolesListQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.Roles.AsNoTracking()
            .OrderByDescending(r => r.IsSystemRole).ThenBy(r => r.Code)
            .Select(r => new
            {
                r.Id, r.Code, r.NameAr, r.NameEn, r.Description, r.IsSystemRole, r.IsActive,
                Users = r.UserRoles.Where(ur => !ur.IsDeleted).Select(ur => ur.UserId).Distinct().Count(),
                Screens = r.ScreenPermissions.Count(p => !p.IsDeleted && p.CanView)
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new RoleListItemDto(
            r.Id, r.Code, r.NameAr, r.NameEn, r.Description, r.IsSystemRole, r.IsActive,
            UserAccess.FullAccessRoles.Contains(r.Code), r.Users, r.Screens)).ToList();
    }
}

public sealed record GetRoleQuery(long Id) : IRequest<RoleDetailDto>;

public sealed class GetRoleQueryHandler(IApplicationDbContext db) : IRequestHandler<GetRoleQuery, RoleDetailDto>
{
    public async Task<RoleDetailDto> Handle(GetRoleQuery request, CancellationToken cancellationToken)
    {
        var role = await db.Roles.AsNoTracking()
            .Include(r => r.ScreenPermissions.Where(p => !p.IsDeleted))
            .Include(r => r.FieldPermissions.Where(p => !p.IsDeleted))
            .Include(r => r.ButtonPermissions.Where(p => !p.IsDeleted))
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.Id);

        return new RoleDetailDto(
            role.Id, role.Code, role.NameAr, role.NameEn, role.Description, role.IsSystemRole, role.IsActive,
            UserAccess.FullAccessRoles.Contains(role.Code),
            role.ScreenPermissions.OrderBy(p => p.ScreenCode)
                .Select(p => new ScreenPermissionDto(p.ScreenCode, p.CanView, p.CanAdd, p.CanEdit, p.CanDelete, p.CanPrint, p.CanExport, p.CanApprove)).ToList(),
            role.FieldPermissions.OrderBy(p => p.ScreenCode).ThenBy(p => p.EntityType).ThenBy(p => p.FieldName)
                .Select(p => new FieldPermissionDto(p.ScreenCode, p.EntityType, p.FieldName, p.CanView, p.CanEdit, p.RequiresAuditLog)).ToList(),
            role.ButtonPermissions.OrderBy(p => p.ScreenCode).ThenBy(p => p.ButtonCode)
                .Select(p => new ButtonPermissionDto(p.ScreenCode, p.ButtonCode, p.IsEnabled, p.RequiresAuditLog)).ToList(),
            Convert.ToBase64String(role.RowVersion));
    }
}

/// <summary>Every screen that can be granted (menu leaves with a route) and every sensitive field.</summary>
public sealed record GetPermissionCatalogQuery : IRequest<PermissionCatalogDto>;

public sealed class GetPermissionCatalogQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPermissionCatalogQuery, PermissionCatalogDto>
{
    public async Task<PermissionCatalogDto> Handle(GetPermissionCatalogQuery request, CancellationToken cancellationToken)
    {
        var items = await db.MenuItems.AsNoTracking().Where(m => m.IsActive).ToListAsync(cancellationToken);
        var groups = items.Where(m => m.ParentId == null).ToDictionary(m => m.Id);

        var screens = items
            .Where(m => m.ParentId != null && !string.IsNullOrEmpty(m.RouteKey))
            .OrderBy(m => groups.GetValueOrDefault(m.ParentId!.Value)?.DisplayOrder).ThenBy(m => m.DisplayOrder)
            .Select(m => new PermissionScreenDto(
                m.Code, m.NameAr, m.NameEn,
                groups.GetValueOrDefault(m.ParentId!.Value)?.NameAr ?? "", groups.GetValueOrDefault(m.ParentId.Value)?.NameEn ?? "", m.RouteKey!))
            .ToList();

        var names = screens.ToDictionary(s => s.Code);
        (string Ar, string En) Name(string code) => names.TryGetValue(code, out var n) ? (n.NameAr, n.NameEn) : (code, code);

        var fieldScreens = FieldPermissionCatalog.SensitiveFields
            .Select(kv => (kv, name: Name(kv.Key)))
            .Select(x => new FieldCatalogScreenDto(
                x.kv.Key, x.name.Ar, x.name.En,
                x.kv.Value.SelectMany(e => e.Value.Select(f => new PermissionFieldDto(e.Key, f))).ToList()))
            .ToList();

        var buttonScreens = ButtonPermissionCatalog.Buttons
            .Select(kv => (kv, name: Name(kv.Key)))
            .Select(x => new ButtonCatalogScreenDto(
                x.kv.Key, x.name.Ar, x.name.En,
                x.kv.Value.Select(b => new PermissionButtonDto(b.Code, b.NameAr, b.NameEn, b.FallbackAction, b.ServerEnforced)).ToList()))
            .ToList();

        return new PermissionCatalogDto(screens, fieldScreens, buttonScreens);
    }
}

// ----------------------------------------------------------------------------------- commands

public sealed record CreateRoleCommand(string Code, string NameAr, string NameEn, string? Description, bool IsActive) : IRequest<long>;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[A-Z0-9_]+$").WithMessage("الكود حروف إنجليزي كبيرة وأرقام و _ بس.");
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class CreateRoleCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current) : IRequestHandler<CreateRoleCommand, long>
{
    public async Task<long> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (await db.Roles.AnyAsync(r => r.Code == request.Code, cancellationToken))
        {
            throw new BusinessRuleException("SET-ROLE-CODE-EXISTS", "فيه دور بنفس الكود في الشركة دي.");
        }

        var role = new Role
        {
            CompanyId = current.CompanyId, Code = request.Code, NameAr = request.NameAr, NameEn = request.NameEn,
            Description = request.Description, IsActive = request.IsActive, IsSystemRole = false
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);
        return role.Id;
    }
}

public sealed record UpdateRoleCommand(long Id, string NameAr, string NameEn, string? Description, bool IsActive, string RowVersion) : IRequest;

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class UpdateRoleCommandHandler(IApplicationDbContext db, IUserAccessService access) : IRequestHandler<UpdateRoleCommand>
{
    public async Task Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await RoleRules.FindAsync(db, request.Id, cancellationToken);
        if (role.IsSystemRole)
        {
            throw new BusinessRuleException("SET-ROLE-SYSTEM", "أدوار النظام مبتتعدّلش (تقدر تعدّل صلاحياتها بس).");
        }

        db.Entry(role).Property(r => r.RowVersion).OriginalValue = Convert.FromBase64String(request.RowVersion);
        role.NameAr = request.NameAr;
        role.NameEn = request.NameEn;
        role.Description = request.Description;
        role.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        access.Invalidate();
    }
}

public sealed record DeleteRoleCommand(long Id) : IRequest;

public sealed class DeleteRoleCommandHandler(IApplicationDbContext db, IUserAccessService access) : IRequestHandler<DeleteRoleCommand>
{
    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await RoleRules.FindAsync(db, request.Id, cancellationToken);
        if (role.IsSystemRole)
        {
            throw new BusinessRuleException("SET-ROLE-SYSTEM", "أدوار النظام مبتتمسحش.");
        }

        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == role.Id, cancellationToken)
            || await db.UserScopes.AnyAsync(s => s.RoleInScope == role.Code, cancellationToken))
        {
            throw new BusinessRuleException("SET-ROLE-IN-USE", "الدور ده متدّي لمستخدمين — شيله منهم الأول.");
        }

        foreach (var p in await db.ScreenPermissions.Where(p => p.RoleId == role.Id).ToListAsync(cancellationToken)) db.ScreenPermissions.Remove(p);
        foreach (var p in await db.FieldPermissions.Where(p => p.RoleId == role.Id).ToListAsync(cancellationToken)) db.FieldPermissions.Remove(p);
        foreach (var p in await db.ButtonPermissions.Where(p => p.RoleId == role.Id).ToListAsync(cancellationToken)) db.ButtonPermissions.Remove(p);
        db.Roles.Remove(role);
        await db.SaveChangesAsync(cancellationToken);
        access.Invalidate();
    }
}

/// <summary>Replaces the role's screen permissions. A screen left out (or with every box unticked) is not granted.</summary>
public sealed record SetRoleScreenPermissionsCommand(long RoleId, IReadOnlyList<ScreenPermissionDto> Screens) : IRequest;

public sealed class SetRoleScreenPermissionsCommandHandler(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<SetRoleScreenPermissionsCommand>
{
    public async Task Handle(SetRoleScreenPermissionsCommand request, CancellationToken cancellationToken)
    {
        var role = await RoleRules.FindEditablePermissionsAsync(db, request.RoleId, cancellationToken);

        var known = (await db.MenuItems.Where(m => m.RouteKey != null && m.ParentId != null).Select(m => m.Code).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = request.Screens.Select(s => s.ScreenCode).Where(c => !known.Contains(c)).ToList();
        if (unknown.Count > 0)
        {
            throw new BusinessRuleException("SET-SCREEN-UNKNOWN", $"شاشات مش موجودة: {string.Join("، ", unknown)}");
        }

        var wanted = request.Screens
            .Where(s => s.CanView || s.CanAdd || s.CanEdit || s.CanDelete || s.CanPrint || s.CanExport || s.CanApprove)
            .GroupBy(s => s.ScreenCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();

        var existing = await db.ScreenPermissions.Where(p => p.RoleId == role.Id).ToListAsync(cancellationToken);
        foreach (var row in existing.Where(e => !wanted.Any(w => string.Equals(w.ScreenCode, e.ScreenCode, StringComparison.OrdinalIgnoreCase))))
        {
            db.ScreenPermissions.Remove(row);
        }

        foreach (var w in wanted)
        {
            var row = existing.FirstOrDefault(e => string.Equals(e.ScreenCode, w.ScreenCode, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                row = new ScreenPermission { CompanyId = current.CompanyId, RoleId = role.Id, ScreenCode = w.ScreenCode };
                db.ScreenPermissions.Add(row);
            }

            // Anything beyond looking needs looking too.
            row.CanView = true;
            row.CanAdd = w.CanAdd;
            row.CanEdit = w.CanEdit;
            row.CanDelete = w.CanDelete;
            row.CanPrint = w.CanPrint;
            row.CanExport = w.CanExport;
            row.CanApprove = w.CanApprove;
        }

        await db.SaveChangesAsync(cancellationToken);
        access.Invalidate();
    }
}

/// <summary>
/// Replaces the role's field permissions on one screen (the others are left alone). A field left out
/// has no restriction for this role on that screen.
/// </summary>
public sealed record SetRoleFieldPermissionsCommand(long RoleId, string ScreenCode, IReadOnlyList<FieldPermissionDto> Fields) : IRequest;

public sealed class SetRoleFieldPermissionsCommandHandler(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<SetRoleFieldPermissionsCommand>
{
    public async Task Handle(SetRoleFieldPermissionsCommand request, CancellationToken cancellationToken)
    {
        var role = await RoleRules.FindEditablePermissionsAsync(db, request.RoleId, cancellationToken);

        var unknown = request.Fields
            .Where(f => f.ScreenCode != request.ScreenCode || !FieldPermissionCatalog.IsListed(request.ScreenCode, f.EntityType, f.FieldName))
            .ToList();
        if (unknown.Count > 0)
        {
            throw new BusinessRuleException(
                "SET-FIELD-UNKNOWN",
                $"حقول مش في قائمة الحقول الحساسة للشاشة دي: {string.Join("، ", unknown.Select(f => $"{f.EntityType}.{f.FieldName}"))}");
        }

        var wanted = request.Fields.GroupBy(f => (f.EntityType, f.FieldName)).Select(g => g.Last()).ToList();
        var existing = await db.FieldPermissions.Where(p => p.RoleId == role.Id && p.ScreenCode == request.ScreenCode).ToListAsync(cancellationToken);

        foreach (var row in existing.Where(e => !wanted.Any(w => w.EntityType == e.EntityType && w.FieldName == e.FieldName)))
        {
            db.FieldPermissions.Remove(row);
        }

        foreach (var w in wanted)
        {
            var row = existing.FirstOrDefault(e => e.EntityType == w.EntityType && e.FieldName == w.FieldName);
            if (row is null)
            {
                row = new FieldPermission
                {
                    CompanyId = current.CompanyId, RoleId = role.Id, ScreenCode = request.ScreenCode, EntityType = w.EntityType, FieldName = w.FieldName
                };
                db.FieldPermissions.Add(row);
            }

            // Editing a field you cannot see makes no sense.
            row.CanView = w.CanView;
            row.CanEdit = w.CanView && w.CanEdit;
            row.RequiresAuditLog = w.RequiresAuditLog;
        }

        await db.SaveChangesAsync(cancellationToken);
        access.Invalidate();
    }
}

/// <summary>
/// Replaces the role's button permissions on one screen. A button left out follows the screen
/// permission it falls back on (ButtonPermissionCatalog).
/// </summary>
public sealed record SetRoleButtonPermissionsCommand(long RoleId, string ScreenCode, IReadOnlyList<ButtonPermissionDto> Buttons) : IRequest;

public sealed class SetRoleButtonPermissionsCommandHandler(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<SetRoleButtonPermissionsCommand>
{
    public async Task Handle(SetRoleButtonPermissionsCommand request, CancellationToken cancellationToken)
    {
        var role = await RoleRules.FindEditablePermissionsAsync(db, request.RoleId, cancellationToken);

        var unknown = request.Buttons
            .Where(b => b.ScreenCode != request.ScreenCode || ButtonPermissionCatalog.Find(request.ScreenCode, b.ButtonCode) is null)
            .ToList();
        if (unknown.Count > 0)
        {
            throw new BusinessRuleException(
                "SET-BUTTON-UNKNOWN", $"أزرار مش في قائمة أزرار الشاشة دي: {string.Join("، ", unknown.Select(b => b.ButtonCode))}");
        }

        var wanted = request.Buttons.GroupBy(b => b.ButtonCode).Select(g => g.Last()).ToList();
        var existing = await db.ButtonPermissions.Where(p => p.RoleId == role.Id && p.ScreenCode == request.ScreenCode).ToListAsync(cancellationToken);

        foreach (var row in existing.Where(e => wanted.All(w => w.ButtonCode != e.ButtonCode)))
        {
            db.ButtonPermissions.Remove(row);
        }

        foreach (var w in wanted)
        {
            var row = existing.FirstOrDefault(e => e.ButtonCode == w.ButtonCode);
            if (row is null)
            {
                row = new ButtonPermission { CompanyId = current.CompanyId, RoleId = role.Id, ScreenCode = request.ScreenCode, ButtonCode = w.ButtonCode };
                db.ButtonPermissions.Add(row);
            }

            row.IsEnabled = w.IsEnabled;
            row.RequiresAuditLog = w.RequiresAuditLog;
        }

        await db.SaveChangesAsync(cancellationToken);
        access.Invalidate();
    }
}

internal static class RoleRules
{
    public static async Task<Role> FindAsync(IApplicationDbContext db, long id, CancellationToken ct) =>
        await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(Role), id);

    public static async Task<Role> FindEditablePermissionsAsync(IApplicationDbContext db, long id, CancellationToken ct)
    {
        var role = await FindAsync(db, id, ct);
        if (UserAccess.FullAccessRoles.Contains(role.Code))
        {
            throw new BusinessRuleException("SET-ROLE-FULL-ACCESS", "الدور ده صلاحياته كاملة دايمًا — مفيش حاجة تتظبط فيه.");
        }

        return role;
    }
}
