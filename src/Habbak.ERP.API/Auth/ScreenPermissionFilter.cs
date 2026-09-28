using System.Reflection;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Habbak.ERP.API.Auth;

/// <summary>
/// The screen(s) a controller belongs to — MenuItem codes. A user needs the permission on any one
/// of them. <see cref="LookupReads"/>: reference data other screens read for their dropdowns
/// (items, accounts, customers…), so any signed-in user may read it; changing it still needs the
/// screen permission.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class ScreenAttribute(params string[] screenCodes) : Attribute
{
    public string[] ScreenCodes { get; } = screenCodes;
    public bool LookupReads { get; init; }
}

/// <summary>Overrides the permission an action needs (otherwise worked out from its HTTP method and route).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ScreenActionAttribute(ScreenAction action) : Attribute
{
    public ScreenAction Action { get; } = action;
}

/// <summary>
/// A special button (ButtonPermissionCatalog) this action is pressed by. A role's ButtonPermission
/// row for it decides alone (enabled + the screen visible); without one the action's normal screen
/// permission applies, which is also what the catalog's FallbackAction says. The same endpoint may
/// be a button on more than one screen: the one the request comes from (X-Screen-Code) is used.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ScreenButtonAttribute(string screenCode, string buttonCode) : Attribute
{
    public string ScreenCode { get; } = screenCode;
    public string ButtonCode { get; } = buttonCode;
}

/// <summary>
/// The screen the request is made from, as ScreenPermissionFilter settled it: the X-Screen-Code
/// header the frontend sends (the open tab) when the user may view that screen, otherwise the
/// controller's own first screen. Null outside a screen-bound controller.
/// </summary>
public sealed class HttpCurrentScreen(IHttpContextAccessor accessor) : ICurrentScreen
{
    public const string HeaderName = "X-Screen-Code";
    internal const string ItemKey = "Habbak.ScreenCode";

    public string? Code => accessor.HttpContext?.Items[ItemKey] as string;
}

/// <summary>A controller any signed-in user may use (navigation, attachments, field labels, their own session).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AnySignedInUserAttribute : Attribute;

/// <summary>
/// Global screen-permission check. Every controller must say which screen it belongs to
/// (<see cref="ScreenAttribute"/>) or that it is open to any signed-in user
/// (<see cref="AnySignedInUserAttribute"/>); a controller that says neither is refused, so a new
/// controller cannot slip through unchecked.
///
/// The permission an action needs: GET → View · DELETE → Delete · PUT → Edit · POST to the
/// controller root → Add · POST approve/reject/post/reverse/cancel… → Approve · any other POST → Edit.
/// Approve-class actions that succeed are written to the audit log. An action marked
/// <see cref="ScreenButtonAttribute"/> is checked against the role's button permission instead, and
/// a successful press is written to the audit log unless the role switched that off.
/// </summary>
public sealed class ScreenPermissionFilter(
    IUserAccessService access, ICurrentUserRoles roles, ICurrentCompanyContext current, IApplicationDbContext db) : IAsyncActionFilter
{
    private static readonly HashSet<string> ApproveSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "approve", "reject", "post", "reverse", "cancel", "approve-close", "settle", "complete-settlement", "award", "reopen", "reassign"
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor action
            || context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()
            || context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var controller = action.ControllerTypeInfo;
        if (roles.PasswordChangeRequired && controller.AsType() != typeof(Controllers.Settings.AuthController))
        {
            throw new ForbiddenException("AUTH-PASSWORD-CHANGE-REQUIRED", "لازم تغيّر كلمة المرور الأول.");
        }

        if (controller.GetCustomAttribute<AnySignedInUserAttribute>() is not null)
        {
            await next();
            return;
        }

        var screen = controller.GetCustomAttribute<ScreenAttribute>(inherit: true)
                     ?? throw new ForbiddenException("SCREEN-NOT-MAPPED", "العملية دي مش مربوطة بشاشة في الصلاحيات.");

        var rights = await access.GetCurrentAsync(context.HttpContext.RequestAborted);
        var effectiveScreen = EffectiveScreen(context.HttpContext, rights, screen);
        context.HttpContext.Items[HttpCurrentScreen.ItemKey] = effectiveScreen;

        var (needed, segment) = Required(action, context.HttpContext.Request.Method);
        var button = PickButton(action, effectiveScreen);
        var buttonRow = button is null ? null : rights.ButtonRow(button.ScreenCode, button.ButtonCode);

        if (button is not null && buttonRow is not null && !rights.IsFullAccess)
        {
            // The role's row decides — it may grant a button beyond the action's own permission, or take it away.
            if (!buttonRow.Enabled || !rights.For(button.ScreenCode).View)
            {
                throw new ForbiddenException("BUTTON-DENIED", "مالكش صلاحية على الزرار ده.");
            }
        }
        else if (!(needed == ScreenAction.View && screen.LookupReads) && !rights.Can(needed, screen.ScreenCodes))
        {
            throw new ForbiddenException("PERMISSION-DENIED", $"مالكش صلاحية {ActionName(needed)} على الشاشة دي.");
        }

        var executed = await next();

        if (executed.Exception is not null || context.HttpContext.Response.StatusCode >= 300)
        {
            return;
        }

        if (button is not null && rights.ButtonAudited(button.ScreenCode, button.ButtonCode))
        {
            await AuditAsync(context, controller, AuditActionType.ButtonPress,
                $"{{\"screen\":\"{button.ScreenCode}\",\"button\":\"{button.ButtonCode}\",\"action\":\"{segment}\"}}");
        }
        else if (needed == ScreenAction.Approve)
        {
            var type = string.Equals(segment, "reject", StringComparison.OrdinalIgnoreCase) ? AuditActionType.Reject : AuditActionType.Approve;
            await AuditAsync(context, controller, type, $"{{\"action\":\"{segment}\"}}");
        }
    }

    /// <summary>The X-Screen-Code header when the user may view that screen, else the controller's first screen.</summary>
    private static string EffectiveScreen(HttpContext http, UserAccess rights, ScreenAttribute screen)
    {
        var header = http.Request.Headers[HttpCurrentScreen.HeaderName].ToString().Trim();
        if (header.Length is > 0 and <= 100 && (rights.IsFullAccess || rights.For(header).View))
        {
            return header;
        }

        return screen.ScreenCodes[0];
    }

    private static ScreenButtonAttribute? PickButton(ControllerActionDescriptor action, string effectiveScreen)
    {
        var buttons = action.MethodInfo.GetCustomAttributes<ScreenButtonAttribute>().ToList();
        return buttons.FirstOrDefault(b => b.ScreenCode == effectiveScreen) ?? buttons.FirstOrDefault();
    }

    public static (ScreenAction Action, string? Segment) Required(ControllerActionDescriptor action, string httpMethod)
    {
        var template = action.AttributeRouteInfo?.Template ?? string.Empty;
        var controllerTemplate = action.ControllerTypeInfo.GetCustomAttribute<RouteAttribute>(inherit: true)?.Template ?? string.Empty;
        var relative = template.Length > controllerTemplate.Length && template.StartsWith(controllerTemplate, StringComparison.OrdinalIgnoreCase)
            ? template[controllerTemplate.Length..].Trim('/')
            : string.Empty;
        var segment = relative.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(s => !s.StartsWith('{'));

        var explicitAction = action.MethodInfo.GetCustomAttribute<ScreenActionAttribute>();
        if (explicitAction is not null)
        {
            return (explicitAction.Action, segment);
        }

        return httpMethod.ToUpperInvariant() switch
        {
            "GET" or "HEAD" => (ScreenAction.View, segment),
            "DELETE" => (ScreenAction.Delete, segment),
            _ when segment is not null && ApproveSegments.Contains(segment) => (ScreenAction.Approve, segment),
            "PUT" or "PATCH" => (ScreenAction.Edit, segment),
            "POST" when relative.Length == 0 => (ScreenAction.Add, segment),
            _ => (ScreenAction.Edit, segment)
        };
    }

    private async Task AuditAsync(ActionExecutingContext context, TypeInfo controller, AuditActionType type, string additionalData)
    {
        long? entityId = context.RouteData.Values.TryGetValue("id", out var raw) && long.TryParse(raw?.ToString(), out var id) ? id : null;
        db.AuditLogs.Add(new AuditLog
        {
            CompanyId = current.CompanyId,
            BranchId = current.BranchId,
            UserId = current.UserId,
            ActionType = type,
            EntityType = controller.Name.Replace("Controller", string.Empty),
            EntityId = entityId,
            IpAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.HttpContext.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua[..Math.Min(ua.Length, 500)] : null,
            OccurredAtUtc = DateTime.UtcNow,
            AdditionalData = additionalData
        });
        await db.SaveChangesAsync(context.HttpContext.RequestAborted);
    }

    private static string ActionName(ScreenAction action) => action switch
    {
        ScreenAction.View => "العرض",
        ScreenAction.Add => "الإضافة",
        ScreenAction.Edit => "التعديل",
        ScreenAction.Delete => "الحذف",
        ScreenAction.Print => "الطباعة",
        ScreenAction.Export => "التصدير",
        _ => "الاعتماد"
    };
}
