using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Habbak.ERP.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps IAuditableEntity fields on every save and turns a hard Remove into a soft delete
/// (00-Project-Overview.md, section 8.4 and section 16) — RowVersion/CreatedAtUtc/UpdatedAtUtc
/// are never set by hand in application code.
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentCompanyContext? currentCompanyContext = null) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>0 when nobody is signed in — a login request writes (attempts, lockout counters) before there is a user claim.</summary>
    private long SafeUserId()
    {
        try
        {
            return currentCompanyContext?.UserId ?? 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        var userId = SafeUserId();

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = utcNow;
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.UpdatedAtUtc = utcNow;
                    entry.Entity.UpdatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = utcNow;
                    entry.Entity.UpdatedBy = userId;
                    break;

                case EntityState.Deleted:
                    // Soft Delete is the default; Hard Delete is exceptional and must be
                    // performed explicitly elsewhere, never implicitly via Remove().
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAtUtc = utcNow;
                    entry.Entity.DeletedBy = userId;
                    entry.Entity.UpdatedAtUtc = utcNow;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }
    }
}
