using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Application.Settings.Auth;
using Habbak.ERP.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Settings.Users;

public sealed record UserListItemDto(
    long Id, string Username, string FullName, string Email, UserStatus Status, DateTime? LastLoginAtUtc,
    bool MustChangePassword, bool TwoFactorEnabled, IReadOnlyList<string> RoleCodes);

public sealed record UserRoleDto(long Id, long RoleId, string RoleCode, string RoleNameAr, string RoleNameEn, long? BranchId, DateTime? ExpiresAtUtc);

public sealed record UserScopeDto(long Id, long CompanyId, long? BranchId, string RoleInScope, bool IsDefault, bool IsActive);

public sealed record UserDetailDto(
    long Id, string Username, string Email, string FullName, string? PhoneNumber, PreferredLanguage PreferredLanguage,
    UserStatus Status, int FailedLoginAttempts, DateTime? LockedUntilUtc, DateTime? LastLoginAtUtc, bool MustChangePassword,
    DateTime? PasswordChangedAtUtc, bool TwoFactorEnabled, IReadOnlyList<UserRoleDto> Roles, IReadOnlyList<UserScopeDto> Scopes, string RowVersion);

public sealed record UserRoleInput(long RoleId, long? BranchId, DateTime? ExpiresAtUtc);

public sealed record UserScopeInput(long CompanyId, long? BranchId, string RoleInScope, bool IsDefault, bool IsActive);

/// <summary>
/// Users are system-wide, but each company manages only the users it can see: a super admin sees
/// everyone; anyone else sees users holding a scope in the current company.
/// </summary>
internal static class UserVisibility
{
    public static async Task<IQueryable<User>> VisibleAsync(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current, CancellationToken ct)
    {
        var rights = await access.GetCurrentAsync(ct);
        if (rights.RoleCodes.Contains(SystemRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase))
        {
            return db.Users.Where(u => u.Id != User.SystemUserId);
        }

        var companyId = current.CompanyId;
        return db.Users.Where(u => u.Id != User.SystemUserId && db.UserScopes.IgnoreQueryFilters().Any(s => s.UserId == u.Id && s.CompanyId == companyId && !s.IsDeleted));
    }

    public static async Task<User> FindAsync(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current, long id, CancellationToken ct) =>
        await (await VisibleAsync(db, access, current, ct)).FirstOrDefaultAsync(u => u.Id == id, ct)
        ?? throw new NotFoundException(nameof(User), id);

    public static async Task<bool> IsSuperAdminAsync(IUserAccessService access, CancellationToken ct) =>
        (await access.GetCurrentAsync(ct)).RoleCodes.Contains(SystemRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase);
}

// ------------------------------------------------------------------------------------ queries

public sealed record GetUsersListQuery(string? Search = null, UserStatus? Status = null) : IRequest<IReadOnlyList<UserListItemDto>>;

public sealed class GetUsersListQueryHandler(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<GetUsersListQuery, IReadOnlyList<UserListItemDto>>
{
    public async Task<IReadOnlyList<UserListItemDto>> Handle(GetUsersListQuery request, CancellationToken cancellationToken)
    {
        var query = (await UserVisibility.VisibleAsync(db, access, current, cancellationToken)).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(u => u.Username.Contains(s) || u.FullName.Contains(s) || u.Email.Contains(s));
        }

        if (request.Status is not null)
        {
            query = query.Where(u => u.Status == request.Status);
        }

        var companyId = current.CompanyId;
        var users = await query.OrderBy(u => u.Username)
            .Select(u => new
            {
                u.Id, u.Username, u.FullName, u.Email, u.Status, u.LastLoginAtUtc, u.MustChangePassword, u.TwoFactorEnabled,
                Roles = u.UserRoles.Where(ur => !ur.IsDeleted && ur.Role.CompanyId == companyId && !ur.Role.IsDeleted).Select(ur => ur.Role.Code).ToList()
            })
            .ToListAsync(cancellationToken);

        return users.Select(u => new UserListItemDto(u.Id, u.Username, u.FullName, u.Email, u.Status, u.LastLoginAtUtc, u.MustChangePassword, u.TwoFactorEnabled, u.Roles.Distinct().ToList())).ToList();
    }
}

public sealed record GetUserQuery(long Id) : IRequest<UserDetailDto>;

public sealed class GetUserQueryHandler(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<GetUserQuery, UserDetailDto>
{
    public async Task<UserDetailDto> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await UserVisibility.FindAsync(db, access, current, request.Id, cancellationToken);
        var superAdmin = await UserVisibility.IsSuperAdminAsync(access, cancellationToken);
        var companyId = current.CompanyId;

        var roles = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == user.Id && ur.Role.CompanyId == companyId)
            .OrderBy(ur => ur.Role.Code)
            .Select(ur => new UserRoleDto(ur.Id, ur.RoleId, ur.Role.Code, ur.Role.NameAr, ur.Role.NameEn, ur.BranchId, ur.ExpiresAtUtc))
            .ToListAsync(cancellationToken);

        var scopes = await db.UserScopes.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.UserId == user.Id && !s.IsDeleted && (superAdmin || s.CompanyId == companyId))
            .OrderBy(s => s.CompanyId).ThenBy(s => s.BranchId)
            .Select(s => new UserScopeDto(s.Id, s.CompanyId!.Value, s.BranchId, s.RoleInScope, s.IsDefault, s.IsActive))
            .ToListAsync(cancellationToken);

        return new UserDetailDto(
            user.Id, user.Username, user.Email, user.FullName, user.PhoneNumber, user.PreferredLanguage, user.Status,
            user.FailedLoginAttempts, user.LockedUntilUtc, user.LastLoginAtUtc, user.MustChangePassword, user.PasswordChangedAtUtc,
            user.TwoFactorEnabled, roles, scopes, Convert.ToBase64String(user.RowVersion));
    }
}

// ----------------------------------------------------------------------------------- commands

public sealed record CreateUserCommand(
    string Username, string Email, string FullName, string? PhoneNumber, PreferredLanguage PreferredLanguage,
    string Password, bool MustChangePassword, IReadOnlyList<long> RoleIds) : IRequest<long>;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("اسم المستخدم حروف إنجليزي وأرقام و . _ - بس.");
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RoleIds).NotEmpty().WithMessage("اختار دور واحد على الأقل.");
    }
}

/// <summary>
/// A new user gets the chosen roles in the current company and a company-wide scope there, whose
/// role is the first one chosen. Scopes in other companies are added from the user screen.
/// </summary>
public sealed class CreateUserCommandHandler(
    IApplicationDbContext db, IPasswordHasher hasher, SessionIssuer sessions, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<CreateUserCommand, long>
{
    public async Task<long> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        await UserRules.EnsureUniqueAsync(db, request.Username, request.Email, null, cancellationToken);
        PasswordRules.Ensure(await sessions.SettingsForAsync(current.CompanyId, cancellationToken), request.Password);

        var roles = await UserRules.CompanyRolesAsync(db, access, request.RoleIds, cancellationToken);
        var now = DateTime.UtcNow;

        var user = new User
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber,
            PreferredLanguage = request.PreferredLanguage,
            PasswordHash = hasher.Hash(request.Password),
            PasswordSalt = string.Empty,
            PasswordChangedAtUtc = now,
            MustChangePassword = request.MustChangePassword,
            Status = UserStatus.Active
        };

        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole { Role = role, AssignedAtUtc = now, AssignedByUserId = current.UserId });
        }

        user.UserScopes.Add(new UserScope
        {
            CompanyId = current.CompanyId, RoleInScope = roles[0].Code, IsDefault = true, IsActive = true,
            GrantedAtUtc = now, GrantedByUserId = current.UserId
        });

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}

public sealed record UpdateUserCommand(
    long Id, string Email, string FullName, string? PhoneNumber, PreferredLanguage PreferredLanguage,
    UserStatus Status, bool MustChangePassword, string RowVersion) : IRequest;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).MaximumLength(50);
        RuleFor(x => x.Status).Must(s => s != UserStatus.Locked).WithMessage("القفل بيحصل لوحده من محاولات الدخول — استخدم \"فك القفل\" بدل ما تختاره.");
    }
}

public sealed class UpdateUserCommandHandler(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<UpdateUserCommand>
{
    public async Task Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await UserVisibility.FindAsync(db, access, current, request.Id, cancellationToken);
        db.Entry(user).Property(u => u.RowVersion).OriginalValue = Convert.FromBase64String(request.RowVersion);
        await UserRules.EnsureUniqueAsync(db, user.Username, request.Email, user.Id, cancellationToken);

        if (user.Id == current.UserId && request.Status != UserStatus.Active)
        {
            throw new BusinessRuleException("SET-USER-SELF-SUSPEND", "مينفعش توقف حسابك انت.");
        }

        user.Email = request.Email.Trim();
        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber;
        user.PreferredLanguage = request.PreferredLanguage;
        user.MustChangePassword = request.MustChangePassword;

        // A locked user keeps the lock until it expires or someone unlocks it.
        if (user.Status != UserStatus.Locked)
        {
            user.Status = request.Status;
        }

        if (user.Status == UserStatus.Suspended)
        {
            await UserRules.RevokeSessionsAsync(db, user.Id, "UserSuspended", cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record ResetUserPasswordCommand(long Id, string NewPassword, bool MustChangePassword = true) : IRequest;

public sealed class ResetUserPasswordCommandHandler(
    IApplicationDbContext db, IPasswordHasher hasher, SessionIssuer sessions, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<ResetUserPasswordCommand>
{
    public async Task Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await UserVisibility.FindAsync(db, access, current, request.Id, cancellationToken);
        PasswordRules.Ensure(await sessions.SettingsForAsync(current.CompanyId, cancellationToken), request.NewPassword);

        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.PasswordSalt = string.Empty;
        user.PasswordChangedAtUtc = DateTime.UtcNow;
        user.MustChangePassword = request.MustChangePassword;
        await UserRules.RevokeSessionsAsync(db, user.Id, "PasswordReset", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record UnlockUserCommand(long Id) : IRequest;

public sealed class UnlockUserCommandHandler(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<UnlockUserCommand>
{
    public async Task Handle(UnlockUserCommand request, CancellationToken cancellationToken)
    {
        var user = await UserVisibility.FindAsync(db, access, current, request.Id, cancellationToken);
        if (user.Status == UserStatus.Locked)
        {
            user.Status = UserStatus.Active;
        }

        user.LockedUntilUtc = null;
        user.FailedLoginAttempts = 0;
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Turns off the user's two-factor sign-in (a lost or replaced phone): the secret and the recovery
/// codes go; the user signs in with the password alone and may set it up again.
/// </summary>
public sealed record ResetUserTwoFactorCommand(long Id) : IRequest;

public sealed class ResetUserTwoFactorCommandHandler(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<ResetUserTwoFactorCommand>
{
    public async Task Handle(ResetUserTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var user = await UserVisibility.FindAsync(db, access, current, request.Id, cancellationToken);
        await Auth.TwoFactorRules.TurnOffAsync(db, user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Replaces the user's roles in the current company with exactly this list.</summary>
public sealed record SetUserRolesCommand(long UserId, IReadOnlyList<UserRoleInput> Roles) : IRequest;

public sealed class SetUserRolesCommandHandler(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<SetUserRolesCommand>
{
    public async Task Handle(SetUserRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await UserVisibility.FindAsync(db, access, current, request.UserId, cancellationToken);
        var distinct = request.Roles.DistinctBy(r => (r.RoleId, r.BranchId)).ToList();
        var roles = await UserRules.CompanyRolesAsync(db, access, distinct.Select(r => r.RoleId).Distinct().ToList(), cancellationToken, allowEmpty: true);

        var existing = await db.UserRoles.Include(ur => ur.Role)
            .Where(ur => ur.UserId == user.Id && ur.Role.CompanyId == current.CompanyId)
            .ToListAsync(cancellationToken);

        static bool Admin(string code) => UserAccess.FullAccessRoles.Contains(code);
        if (user.Id == current.UserId && existing.Any(ur => Admin(ur.Role.Code)) && !roles.Any(r => Admin(r.Code)))
        {
            throw new BusinessRuleException("SET-USER-SELF-DEMOTE", "مينفعش تشيل صلاحية الإدارة من نفسك.");
        }

        var now = DateTime.UtcNow;
        foreach (var row in existing.Where(ur => !distinct.Any(d => d.RoleId == ur.RoleId && d.BranchId == ur.BranchId)))
        {
            db.UserRoles.Remove(row);
        }

        foreach (var input in distinct)
        {
            var row = existing.FirstOrDefault(ur => ur.RoleId == input.RoleId && ur.BranchId == input.BranchId);
            if (row is null)
            {
                db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id, RoleId = input.RoleId, BranchId = input.BranchId, ExpiresAtUtc = input.ExpiresAtUtc,
                    AssignedAtUtc = now, AssignedByUserId = current.UserId
                });
            }
            else
            {
                row.ExpiresAtUtc = input.ExpiresAtUtc;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        access.Invalidate();
    }
}

/// <summary>Replaces the user's scopes — in the current company, or in every company for a super admin.</summary>
public sealed record SetUserScopesCommand(long UserId, IReadOnlyList<UserScopeInput> Scopes) : IRequest;

public sealed class SetUserScopesCommandValidator : AbstractValidator<SetUserScopesCommand>
{
    public SetUserScopesCommandValidator()
    {
        RuleForEach(x => x.Scopes).ChildRules(s =>
        {
            s.RuleFor(x => x.CompanyId).GreaterThan(0);
            s.RuleFor(x => x.RoleInScope).NotEmpty().MaximumLength(50);
        });
        RuleFor(x => x.Scopes).Must(s => s.Count(x => x.IsDefault) <= 1).WithMessage("نطاق افتراضي واحد بس.");
        RuleFor(x => x.Scopes).Must(s => s.GroupBy(x => (x.CompanyId, x.BranchId)).All(g => g.Count() == 1))
            .WithMessage("نفس الشركة والفرع متكررين.");
    }
}

public sealed class SetUserScopesCommandHandler(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<SetUserScopesCommand>
{
    public async Task Handle(SetUserScopesCommand request, CancellationToken cancellationToken)
    {
        var user = await UserVisibility.FindAsync(db, access, current, request.UserId, cancellationToken);
        var superAdmin = await UserVisibility.IsSuperAdminAsync(access, cancellationToken);

        if (!superAdmin && request.Scopes.Any(s => s.CompanyId != current.CompanyId))
        {
            throw new ForbiddenException("SET-SCOPE-OTHER-COMPANY", "تقدر تدير نطاقات الشركة الحالية بس.");
        }

        var companyIds = request.Scopes.Select(s => s.CompanyId).Distinct().ToList();
        var known = await db.Companies.IgnoreQueryFilters().Where(c => companyIds.Contains(c.Id) && !c.IsDeleted).Select(c => c.Id).ToListAsync(cancellationToken);
        if (known.Count != companyIds.Count)
        {
            throw new BusinessRuleException("SET-SCOPE-UNKNOWN-COMPANY", "فيه شركة في النطاقات مش موجودة.");
        }

        foreach (var scope in request.Scopes)
        {
            var roleExists = await db.Roles.IgnoreQueryFilters().AnyAsync(r => r.CompanyId == scope.CompanyId && r.Code == scope.RoleInScope && !r.IsDeleted, cancellationToken);
            if (!roleExists)
            {
                throw new BusinessRuleException("SET-SCOPE-UNKNOWN-ROLE", $"الدور {scope.RoleInScope} مش موجود في الشركة {scope.CompanyId}.");
            }

            if (!superAdmin && scope.RoleInScope == SystemRoles.SuperAdmin)
            {
                throw new ForbiddenException("SET-SUPER-ADMIN-ONLY", "مدير النظام بس اللي يدّي دور مدير النظام.");
            }
        }

        var existing = await db.UserScopes.IgnoreQueryFilters()
            .Where(s => s.UserId == user.Id && !s.IsDeleted && (superAdmin || s.CompanyId == current.CompanyId))
            .ToListAsync(cancellationToken);

        if (user.Id == current.UserId && !request.Scopes.Any(s => s.CompanyId == current.CompanyId && s.IsActive))
        {
            throw new BusinessRuleException("SET-SCOPE-SELF-LOCKOUT", "مينفعش تشيل نطاقك على الشركة اللي انت داخل عليها.");
        }

        var now = DateTime.UtcNow;
        foreach (var row in existing.Where(e => !request.Scopes.Any(s => s.CompanyId == e.CompanyId && s.BranchId == e.BranchId)))
        {
            db.UserScopes.Remove(row);
        }

        foreach (var input in request.Scopes)
        {
            var row = existing.FirstOrDefault(e => e.CompanyId == input.CompanyId && e.BranchId == input.BranchId);
            if (row is null)
            {
                db.UserScopes.Add(new UserScope
                {
                    UserId = user.Id, CompanyId = input.CompanyId, BranchId = input.BranchId, RoleInScope = input.RoleInScope,
                    IsDefault = input.IsDefault, IsActive = input.IsActive, GrantedAtUtc = now, GrantedByUserId = current.UserId
                });
            }
            else
            {
                row.RoleInScope = input.RoleInScope;
                row.IsDefault = input.IsDefault;
                row.IsActive = input.IsActive;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

internal static class UserRules
{
    public static async Task EnsureUniqueAsync(IApplicationDbContext db, string username, string email, long? exceptId, CancellationToken ct)
    {
        var u = username.Trim();
        var e = email.Trim();
        if (await db.Users.AnyAsync(x => x.Username == u && x.Id != exceptId, ct))
        {
            throw new BusinessRuleException("SET-USERNAME-EXISTS", "اسم المستخدم ده مستخدم قبل كده.");
        }

        if (await db.Users.AnyAsync(x => x.Email == e && x.Id != exceptId, ct))
        {
            throw new BusinessRuleException("SET-EMAIL-EXISTS", "الإيميل ده مستخدم قبل كده.");
        }
    }

    /// <summary>Roles of the current company, all found, and SUPER_ADMIN only granted by a super admin.</summary>
    public static async Task<List<Role>> CompanyRolesAsync(
        IApplicationDbContext db, IUserAccessService access, IReadOnlyList<long> roleIds, CancellationToken ct, bool allowEmpty = false)
    {
        var roles = await db.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(ct);
        if (roles.Count != roleIds.Distinct().Count() || (!allowEmpty && roles.Count == 0))
        {
            throw new BusinessRuleException("SET-ROLE-NOT-FOUND", "فيه دور مش موجود في الشركة دي.");
        }

        if (roles.Any(r => !r.IsActive))
        {
            throw new BusinessRuleException("SET-ROLE-INACTIVE", "فيه دور متوقف.");
        }

        if (roles.Any(r => r.Code == SystemRoles.SuperAdmin) && !await UserVisibility.IsSuperAdminAsync(access, ct))
        {
            throw new ForbiddenException("SET-SUPER-ADMIN-ONLY", "مدير النظام بس اللي يدّي دور مدير النظام.");
        }

        return roleIds.Select(id => roles.First(r => r.Id == id)).ToList();
    }

    public static async Task RevokeSessionsAsync(IApplicationDbContext db, long userId, string reason, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var t in await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(ct))
        {
            t.RevokedAtUtc = now;
            t.RevokedReason = reason;
        }
    }
}
