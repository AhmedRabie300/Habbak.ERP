using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Settings;

namespace Habbak.ERP.Application.Settings.Access;

/// <summary>
/// For the special buttons that are not an endpoint of their own — editing a line's price and
/// voiding a line already sent to the kitchen ride on the ordinary line update/delete — so the
/// handler checks the button and records the press itself ([ScreenButton] does both for the rest).
/// </summary>
public static class ButtonGuard
{
    public static async Task RequireAsync(IUserAccessService access, string screenCode, string buttonCode, CancellationToken cancellationToken)
    {
        if (!await access.HasButtonPermissionAsync(screenCode, buttonCode, cancellationToken))
        {
            throw new ForbiddenException("BUTTON-DENIED", "مالكش صلاحية على الزرار ده.");
        }
    }

    /// <summary>Adds the press to the audit log (saved with the caller's own SaveChanges) unless the role switched that off.</summary>
    public static async Task AuditAsync(
        IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current,
        string screenCode, string buttonCode, string entityType, long? entityId, CancellationToken cancellationToken)
    {
        if (!await access.ShouldAuditButtonAsync(screenCode, buttonCode, cancellationToken))
        {
            return;
        }

        db.AuditLogs.Add(new AuditLog
        {
            CompanyId = current.CompanyId,
            BranchId = current.BranchId,
            UserId = current.UserId,
            ActionType = AuditActionType.ButtonPress,
            EntityType = entityType,
            EntityId = entityId,
            OccurredAtUtc = DateTime.UtcNow,
            AdditionalData = $"{{\"screen\":\"{screenCode}\",\"button\":\"{buttonCode}\"}}"
        });
    }
}
