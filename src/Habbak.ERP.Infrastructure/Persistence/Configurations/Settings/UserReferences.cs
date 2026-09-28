using System.Linq.Expressions;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.FixedAssets;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Notifications;
using Habbak.ERP.Domain.POS;
using Habbak.ERP.Domain.Posting;
using Habbak.ERP.Domain.Purchasing;
using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Settings;

/// <summary>
/// Every column that holds a user id points at Users (Settings &amp; Permissions, phase 3) — the one
/// list of them, so a new user column is added here and nowhere else. User 0 is the system
/// (User.SystemUserId), which is what the audit columns hold for anything done before sign-in.
///
/// Deliberately left without a key: AuditLog.UserId and LoginAttempt.UserId (the audit trail must
/// outlive any user row, and an attempt may name no user at all), and CustodyRegister.EmployeeId
/// (an employee, once HR exists — not a user).
/// </summary>
public static class UserReferences
{
    private static readonly string[] AuditColumns =
        [nameof(IAuditableEntity.CreatedBy), nameof(IAuditableEntity.UpdatedBy), nameof(IAuditableEntity.DeletedBy)];

    public static void Apply(ModelBuilder modelBuilder)
    {
        // Who did what: indexed for "everything user X did" queries.
        Reference<AccountingPeriod>(modelBuilder, p => p.ClosedByUserId);
        Reference<BankReconciliationRun>(modelBuilder, r => r.ImportedByUserId);
        Reference<CashReconciliation>(modelBuilder, r => r.ApprovedByUserId);
        Reference<JournalEntry>(modelBuilder, e => e.PostedBy);
        Reference<BranchRequest>(modelBuilder, r => r.RequestedByUserId);
        Reference<ProductionOrder>(modelBuilder, o => o.ExecutedByUserId);
        Reference<PurchaseRequest>(modelBuilder, r => r.RequestedByUserId);
        Reference<CheckLineVoid>(modelBuilder, v => v.VoidedByUserId);
        Reference<DrawerMovement>(modelBuilder, m => m.ApprovedByUserId);
        Reference<Shift>(modelBuilder, s => s.CashierUserId);
        Reference<Shift>(modelBuilder, s => s.ClosedByUserId);
        Reference<DepreciationRun>(modelBuilder, r => r.PostedByUserId);
        Reference<DepreciationRun>(modelBuilder, r => r.ReversedByUserId);
        Reference<MaintenanceIssue>(modelBuilder, i => i.ReportedByUserId);
        Reference<ShiftAssignment>(modelBuilder, a => a.UserId);
        Reference<PostingFailure>(modelBuilder, f => f.UserId);
        Reference<UserRole>(modelBuilder, r => r.AssignedByUserId);
        Reference<UserScope>(modelBuilder, s => s.GrantedByUserId);
        Reference<Notification>(modelBuilder, n => n.RecipientUserId);

        // The audit columns every table carries — unindexed, see AuditColumnsForeignKeyIndexConvention.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(IAuditableEntity).IsAssignableFrom(t.ClrType) && !t.IsOwned())
                     .ToList())
        {
            var builder = modelBuilder.Entity(entityType.ClrType);
            foreach (var column in AuditColumns)
            {
                builder.HasOne(typeof(User)).WithMany().HasForeignKey(column).OnDelete(DeleteBehavior.Restrict);
            }
        }
    }

    private static void Reference<T>(ModelBuilder modelBuilder, Expression<Func<T, object?>> column) where T : class =>
        modelBuilder.Entity<T>().HasOne<User>().WithMany().HasForeignKey(column).OnDelete(DeleteBehavior.Restrict);
}
