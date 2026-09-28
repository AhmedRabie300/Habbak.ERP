using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Settings.Roles;
using Habbak.ERP.Application.Settings.Security;
using Habbak.ERP.Application.Settings.Users;
using Habbak.ERP.Domain.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Settings;

/// <summary>/settings/users — users, their roles in this company and their scopes.</summary>
[ApiController]
[Authorize]
[Screen(SecurityScreens.Users)]
[Route("api/v1/settings/users")]
public class UsersController(ISender mediator) : ControllerBase
{
    public sealed record CreateUserRequest(
        string Username, string Email, string FullName, string? PhoneNumber, PreferredLanguage PreferredLanguage,
        string Password, bool MustChangePassword, IReadOnlyList<long> RoleIds);
    public sealed record UpdateUserRequest(
        string Email, string FullName, string? PhoneNumber, PreferredLanguage PreferredLanguage, UserStatus Status, bool MustChangePassword, string RowVersion);
    public sealed record ResetPasswordRequest(string NewPassword, bool MustChangePassword = true);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] string? search, [FromQuery] UserStatus? status, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetUsersListQuery(search, status), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetUserQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest r, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreateUserCommand(r.Username, r.Email, r.FullName, r.PhoneNumber, r.PreferredLanguage, r.Password, r.MustChangePassword, r.RoleIds), cancellationToken) });

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateUserRequest r, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateUserCommand(id, r.Email, r.FullName, r.PhoneNumber, r.PreferredLanguage, r.Status, r.MustChangePassword, r.RowVersion), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(long id, [FromBody] ResetPasswordRequest r, CancellationToken cancellationToken)
    {
        await mediator.Send(new ResetUserPasswordCommand(id, r.NewPassword, r.MustChangePassword), cancellationToken);
        return NoContent();
    }

    /// <summary>Turns off the user's two-factor sign-in (lost phone).</summary>
    [HttpPost("{id:long}/reset-two-factor")]
    public async Task<IActionResult> ResetTwoFactor(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ResetUserTwoFactorCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/unlock")]
    public async Task<IActionResult> Unlock(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new UnlockUserCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:long}/roles")]
    public async Task<IActionResult> SetRoles(long id, [FromBody] IReadOnlyList<UserRoleInput> roles, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetUserRolesCommand(id, roles), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:long}/scopes")]
    public async Task<IActionResult> SetScopes(long id, [FromBody] IReadOnlyList<UserScopeInput> scopes, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetUserScopesCommand(id, scopes), cancellationToken);
        return NoContent();
    }
}

/// <summary>/settings/users-lookup — names of the company's users, for any screen that shows who did something.</summary>
[ApiController]
[Authorize]
[AnySignedInUser]
[Route("api/v1/settings/users-lookup")]
public class UsersLookupController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await mediator.Send(new GetUsersLookupQuery(), cancellationToken));
}

/// <summary>/settings/roles — roles with their screen and field permissions.</summary>
[ApiController]
[Authorize]
[Screen(SecurityScreens.Roles, LookupReads = true)]
[Route("api/v1/settings/roles")]
public class RolesController(ISender mediator) : ControllerBase
{
    public sealed record CreateRoleRequest(string Code, string NameAr, string NameEn, string? Description, bool IsActive);
    public sealed record UpdateRoleRequest(string NameAr, string NameEn, string? Description, bool IsActive, string RowVersion);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) => Ok(await mediator.Send(new GetRolesListQuery(), cancellationToken));

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(CancellationToken cancellationToken) => Ok(await mediator.Send(new GetPermissionCatalogQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken) => Ok(await mediator.Send(new GetRoleQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest r, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreateRoleCommand(r.Code, r.NameAr, r.NameEn, r.Description, r.IsActive), cancellationToken) });

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateRoleRequest r, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateRoleCommand(id, r.NameAr, r.NameEn, r.Description, r.IsActive, r.RowVersion), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteRoleCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:long}/screens")]
    public async Task<IActionResult> SetScreens(long id, [FromBody] IReadOnlyList<ScreenPermissionDto> screens, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetRoleScreenPermissionsCommand(id, screens), cancellationToken);
        return NoContent();
    }

    /// <summary>Replaces the role's field permissions on one screen.</summary>
    [HttpPut("{id:long}/fields")]
    public async Task<IActionResult> SetFields(
        long id, [FromQuery] string screenCode, [FromBody] IReadOnlyList<FieldPermissionDto> fields, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetRoleFieldPermissionsCommand(id, screenCode, fields), cancellationToken);
        return NoContent();
    }

    /// <summary>Replaces the role's button permissions on one screen.</summary>
    [HttpPut("{id:long}/buttons")]
    public async Task<IActionResult> SetButtons(
        long id, [FromQuery] string screenCode, [FromBody] IReadOnlyList<ButtonPermissionDto> buttons, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetRoleButtonPermissionsCommand(id, screenCode, buttons), cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// /permissions — the field and button catalogs per screen, and what a role has on them. Read by the
/// roles screen; the same permission as it.
/// </summary>
[ApiController]
[Authorize]
[Screen(SecurityScreens.Roles)]
[Route("api/v1/permissions")]
public class PermissionsController(ISender mediator) : ControllerBase
{
    [HttpGet("field-catalog")]
    public async Task<IActionResult> FieldCatalog([FromQuery] string? screenCode, CancellationToken cancellationToken)
    {
        var catalog = await mediator.Send(new GetPermissionCatalogQuery(), cancellationToken);
        return Ok(catalog.FieldScreens.Where(s => screenCode == null || s.ScreenCode == screenCode));
    }

    [HttpGet("button-catalog")]
    public async Task<IActionResult> ButtonCatalog([FromQuery] string? screenCode, CancellationToken cancellationToken)
    {
        var catalog = await mediator.Send(new GetPermissionCatalogQuery(), cancellationToken);
        return Ok(catalog.ButtonScreens.Where(s => screenCode == null || s.ScreenCode == screenCode));
    }

    [HttpGet("role/{roleId:long}/fields")]
    public async Task<IActionResult> RoleFields(long roleId, [FromQuery] string? screenCode, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetRoleFieldRightsQuery(roleId, screenCode), cancellationToken));

    [HttpGet("role/{roleId:long}/buttons")]
    public async Task<IActionResult> RoleButtons(long roleId, [FromQuery] string? screenCode, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetRoleButtonRightsQuery(roleId, screenCode), cancellationToken));
}

[ApiController]
[Authorize]
[Screen(SecurityScreens.Sessions)]
[Route("api/v1/settings/sessions")]
public class SessionsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) => Ok(await mediator.Send(new GetActiveSessionsQuery(), cancellationToken));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Revoke(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RevokeSessionCommand(id), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Screen(SecurityScreens.LoginAttempts)]
[Route("api/v1/settings/login-attempts")]
public class LoginAttemptsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? username, [FromQuery] bool? success, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetLoginAttemptsQuery(username, success, fromUtc, toUtc, page, pageSize), cancellationToken));
}

[ApiController]
[Authorize]
[Screen(SecurityScreens.AuditLog)]
[Route("api/v1/settings/audit-log")]
public class AuditLogController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] long? userId, [FromQuery] string? entityType, [FromQuery] long? entityId, [FromQuery] AuditActionType? actionType,
        [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetAuditLogsQuery(userId, entityType, entityId, actionType, fromUtc, toUtc, page, pageSize), cancellationToken));

    [HttpGet("entity-types")]
    public async Task<IActionResult> EntityTypes(CancellationToken cancellationToken) => Ok(await mediator.Send(new GetAuditEntityTypesQuery(), cancellationToken));
}

[ApiController]
[Authorize]
[Screen(SecurityScreens.SecuritySettings)]
[Route("api/v1/settings/security")]
public class SecuritySettingsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await mediator.Send(new GetSystemSettingsQuery(), cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] SystemSettingsDto settings, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSystemSettingsCommand(settings), cancellationToken);
        return NoContent();
    }
}

public static class SecurityScreens
{
    public const string Users = "SETTINGS_USERS";
    public const string Roles = "SETTINGS_ROLES";
    public const string Sessions = "SETTINGS_SESSIONS";
    public const string LoginAttempts = "SETTINGS_LOGIN_ATTEMPTS";
    public const string AuditLog = "SETTINGS_AUDIT_LOG";
    public const string SecuritySettings = "SETTINGS_SECURITY";
}
