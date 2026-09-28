using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Habbak.ERP.Application.Settings.Access;

/// <summary>
/// Whether a change to a sensitive field by the current user goes to the audit log
/// (FieldPermission.RequiresAuditLog on the request's screen — on unless every one of the user's
/// roles switched it off there).
/// Asked by AppDbContext while saving; resolves the access service lazily because that service
/// itself reads through the same context.
/// </summary>
public interface IAuditFieldPolicy
{
    Task<bool> ShouldAuditAsync(string entityType, string fieldName, CancellationToken cancellationToken);
}

public sealed class AuditFieldPolicy(IServiceProvider services) : IAuditFieldPolicy
{
    public async Task<bool> ShouldAuditAsync(string entityType, string fieldName, CancellationToken cancellationToken)
    {
        if (!FieldPermissionCatalog.IsSensitive(entityType, fieldName))
        {
            return true;
        }

        try
        {
            var access = await services.GetRequiredService<IUserAccessService>().GetCurrentAsync(cancellationToken);
            return access.FieldOn(services.GetRequiredService<ICurrentScreen>().Code, entityType, fieldName).Audit;
        }
        catch (InvalidOperationException)
        {
            // No signed-in user (startup, background work): audit.
            return true;
        }
    }
}
