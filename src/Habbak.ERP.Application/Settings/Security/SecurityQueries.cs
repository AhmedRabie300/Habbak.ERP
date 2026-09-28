using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Settings.Security;

// ---------------------------------------------------------------------------- active sessions

public sealed record ActiveSessionDto(long Id, long UserId, string Username, string FullName, DateTime CreatedAtUtc, DateTime ExpiresAtUtc, string CreatedByIp, string? UserAgent);

public sealed record GetActiveSessionsQuery : IRequest<IReadOnlyList<ActiveSessionDto>>;

/// <summary>Unexpired, unrevoked refresh tokens — of users with a scope in this company (every user for a super admin).</summary>
public sealed class GetActiveSessionsQueryHandler(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<GetActiveSessionsQuery, IReadOnlyList<ActiveSessionDto>>
{
    public async Task<IReadOnlyList<ActiveSessionDto>> Handle(GetActiveSessionsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var all = (await access.GetCurrentAsync(cancellationToken)).RoleCodes.Contains(SystemRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase);
        var companyId = current.CompanyId;

        return await db.RefreshTokens.AsNoTracking()
            .Where(t => t.RevokedAtUtc == null && t.ExpiresAtUtc > now)
            .Where(t => all || db.UserScopes.IgnoreQueryFilters().Any(s => s.UserId == t.UserId && s.CompanyId == companyId && !s.IsDeleted))
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new ActiveSessionDto(t.Id, t.UserId, t.User.Username, t.User.FullName, t.CreatedAtUtc, t.ExpiresAtUtc, t.CreatedByIp, t.UserAgent))
            .ToListAsync(cancellationToken);
    }
}

public sealed record RevokeSessionCommand(long Id) : IRequest;

public sealed class RevokeSessionCommandHandler(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current) : IRequestHandler<RevokeSessionCommand>
{
    public async Task Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var all = (await access.GetCurrentAsync(cancellationToken)).RoleCodes.Contains(SystemRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase);
        var companyId = current.CompanyId;
        var token = await db.RefreshTokens
            .Where(t => t.Id == request.Id)
            .Where(t => all || db.UserScopes.IgnoreQueryFilters().Any(s => s.UserId == t.UserId && s.CompanyId == companyId && !s.IsDeleted))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(RefreshToken), request.Id);

        if (token.RevokedAtUtc is null)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            token.RevokedReason = "RevokedByAdmin";
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

// ----------------------------------------------------------------------------- login attempts

public sealed record LoginAttemptDto(long Id, long? UserId, string Username, bool Success, string IpAddress, string? UserAgent, string? FailureReason, DateTime AttemptedAtUtc);

public sealed record GetLoginAttemptsQuery(string? Username, bool? Success, DateTime? FromUtc, DateTime? ToUtc, int Page = 1, int PageSize = 50)
    : IRequest<PagedResult<LoginAttemptDto>>;

/// <summary>A super admin sees every attempt; anyone else sees attempts on usernames of users in this company.</summary>
public sealed class GetLoginAttemptsQueryHandler(IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current)
    : IRequestHandler<GetLoginAttemptsQuery, PagedResult<LoginAttemptDto>>
{
    public async Task<PagedResult<LoginAttemptDto>> Handle(GetLoginAttemptsQuery request, CancellationToken cancellationToken)
    {
        var all = (await access.GetCurrentAsync(cancellationToken)).RoleCodes.Contains(SystemRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase);
        var companyId = current.CompanyId;
        var query = db.LoginAttempts.AsNoTracking().AsQueryable();

        if (!all)
        {
            query = query.Where(a => a.UserId != null
                                     && db.UserScopes.IgnoreQueryFilters().Any(s => s.UserId == a.UserId && s.CompanyId == companyId && !s.IsDeleted));
        }

        if (!string.IsNullOrWhiteSpace(request.Username)) query = query.Where(a => a.Username.Contains(request.Username.Trim()));
        if (request.Success is not null) query = query.Where(a => a.Success == request.Success);
        if (request.FromUtc is not null) query = query.Where(a => a.AttemptedAtUtc >= request.FromUtc);
        if (request.ToUtc is not null) query = query.Where(a => a.AttemptedAtUtc < request.ToUtc);

        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 200);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(a => a.AttemptedAtUtc).Skip((page - 1) * size).Take(size)
            .Select(a => new LoginAttemptDto(a.Id, a.UserId, a.Username, a.Success, a.IpAddress, a.UserAgent, a.FailureReason, a.AttemptedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<LoginAttemptDto> { Items = items, TotalCount = total, Page = page, PageSize = size };
    }
}

// ---------------------------------------------------------------------------------- audit log

public sealed record AuditLogDto(
    long Id, long UserId, string? Username, AuditActionType ActionType, string EntityType, long? EntityId, string? FieldName,
    string? OldValue, string? NewValue, string? IpAddress, DateTime OccurredAtUtc, string? AdditionalData);

public sealed record GetAuditLogsQuery(
    long? UserId, string? EntityType, long? EntityId, AuditActionType? ActionType, DateTime? FromUtc, DateTime? ToUtc, int Page = 1, int PageSize = 50)
    : IRequest<PagedResult<AuditLogDto>>;

/// <summary>This company's entries. The log has no query filter of its own, so the company is filtered here.</summary>
public sealed class GetAuditLogsQueryHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public async Task<PagedResult<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var companyId = current.CompanyId;
        var query = db.AuditLogs.AsNoTracking().Where(a => a.CompanyId == companyId);

        // The log has no query filter, so the branch scope is applied here too.
        if (current.BranchId is { } branchId)
        {
            query = query.Where(a => a.BranchId == null || a.BranchId == branchId);
        }

        if (request.UserId is not null) query = query.Where(a => a.UserId == request.UserId);
        if (!string.IsNullOrWhiteSpace(request.EntityType)) query = query.Where(a => a.EntityType == request.EntityType);
        if (request.EntityId is not null) query = query.Where(a => a.EntityId == request.EntityId);
        if (request.ActionType is not null) query = query.Where(a => a.ActionType == request.ActionType);
        if (request.FromUtc is not null) query = query.Where(a => a.OccurredAtUtc >= request.FromUtc);
        if (request.ToUtc is not null) query = query.Where(a => a.OccurredAtUtc < request.ToUtc);

        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 200);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(a => a.Id).Skip((page - 1) * size).Take(size)
            .Select(a => new AuditLogDto(
                a.Id, a.UserId, db.Users.Where(u => u.Id == a.UserId).Select(u => u.Username).FirstOrDefault(),
                a.ActionType, a.EntityType, a.EntityId, a.FieldName, a.OldValue, a.NewValue, a.IpAddress, a.OccurredAtUtc, a.AdditionalData))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto> { Items = items, TotalCount = total, Page = page, PageSize = size };
    }
}

/// <summary>The entity types that appear in this company's log, for the filter dropdown.</summary>
public sealed record GetAuditEntityTypesQuery : IRequest<IReadOnlyList<string>>;

public sealed class GetAuditEntityTypesQueryHandler(IApplicationDbContext db, ICurrentCompanyContext current) : IRequestHandler<GetAuditEntityTypesQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(GetAuditEntityTypesQuery request, CancellationToken cancellationToken)
    {
        var companyId = current.CompanyId;
        return await db.AuditLogs.AsNoTracking().Where(a => a.CompanyId == companyId).Select(a => a.EntityType).Distinct().OrderBy(t => t).ToListAsync(cancellationToken);
    }
}

// ---------------------------------------------------------------------------- system settings

public sealed record SystemSettingsDto(
    int PasswordMinLength, bool PasswordRequireUppercase, bool PasswordRequireLowercase, bool PasswordRequireDigit, bool PasswordRequireSpecial,
    int PasswordExpiryDays, int MaxFailedLoginAttempts, int AccountLockoutMinutes, int SessionTimeoutMinutes, int RefreshTokenExpiryDays,
    int AuditRetentionYears, string? RowVersion);

public sealed record GetSystemSettingsQuery : IRequest<SystemSettingsDto>;

public sealed class GetSystemSettingsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSystemSettingsQuery, SystemSettingsDto>
{
    public async Task<SystemSettingsDto> Handle(GetSystemSettingsQuery request, CancellationToken cancellationToken)
    {
        var s = await db.SystemSettingsRows.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return SystemSettingsMapping.ToDto(s ?? new SystemSettings(), s is null ? null : Convert.ToBase64String(s.RowVersion));
    }
}

public sealed record UpdateSystemSettingsCommand(SystemSettingsDto Settings) : IRequest;

public sealed class UpdateSystemSettingsCommandValidator : AbstractValidator<UpdateSystemSettingsCommand>
{
    public UpdateSystemSettingsCommandValidator()
    {
        RuleFor(x => x.Settings.PasswordMinLength).InclusiveBetween(6, 64);
        RuleFor(x => x.Settings.PasswordExpiryDays).InclusiveBetween(0, 3650);
        RuleFor(x => x.Settings.MaxFailedLoginAttempts).InclusiveBetween(1, 50);
        RuleFor(x => x.Settings.AccountLockoutMinutes).InclusiveBetween(1, 1440);
        RuleFor(x => x.Settings.SessionTimeoutMinutes).InclusiveBetween(5, 720);
        RuleFor(x => x.Settings.RefreshTokenExpiryDays).InclusiveBetween(1, 90);
        RuleFor(x => x.Settings.AuditRetentionYears).InclusiveBetween(1, 50);
    }
}

public sealed class UpdateSystemSettingsCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current) : IRequestHandler<UpdateSystemSettingsCommand>
{
    public async Task Handle(UpdateSystemSettingsCommand request, CancellationToken cancellationToken)
    {
        var s = await db.SystemSettingsRows.FirstOrDefaultAsync(cancellationToken);
        if (s is null)
        {
            s = new SystemSettings { CompanyId = current.CompanyId };
            db.SystemSettingsRows.Add(s);
        }
        else if (request.Settings.RowVersion is not null)
        {
            db.Entry(s).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(request.Settings.RowVersion);
        }

        var d = request.Settings;
        s.PasswordMinLength = d.PasswordMinLength;
        s.PasswordRequireUppercase = d.PasswordRequireUppercase;
        s.PasswordRequireLowercase = d.PasswordRequireLowercase;
        s.PasswordRequireDigit = d.PasswordRequireDigit;
        s.PasswordRequireSpecial = d.PasswordRequireSpecial;
        s.PasswordExpiryDays = d.PasswordExpiryDays;
        s.MaxFailedLoginAttempts = d.MaxFailedLoginAttempts;
        s.AccountLockoutMinutes = d.AccountLockoutMinutes;
        s.SessionTimeoutMinutes = d.SessionTimeoutMinutes;
        s.RefreshTokenExpiryDays = d.RefreshTokenExpiryDays;
        s.AuditRetentionYears = d.AuditRetentionYears;
        await db.SaveChangesAsync(cancellationToken);
    }
}

internal static class SystemSettingsMapping
{
    public static SystemSettingsDto ToDto(SystemSettings s, string? rowVersion) => new(
        s.PasswordMinLength, s.PasswordRequireUppercase, s.PasswordRequireLowercase, s.PasswordRequireDigit, s.PasswordRequireSpecial,
        s.PasswordExpiryDays, s.MaxFailedLoginAttempts, s.AccountLockoutMinutes, s.SessionTimeoutMinutes, s.RefreshTokenExpiryDays,
        s.AuditRetentionYears, rowVersion);
}
