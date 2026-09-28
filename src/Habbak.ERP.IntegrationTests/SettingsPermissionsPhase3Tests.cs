using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests;

/// <summary>
/// Settings &amp; Permissions, phase 3 (Docs/Modules/Settings-Permissions-Phase3.md): user columns are
/// real foreign keys, the system user they fall back on, and the daily maintenance job.
/// </summary>
public class SettingsPermissionsPhase3Tests(PostingServiceFixture fixture) : IClassFixture<PostingServiceFixture>
{
    [Fact]
    public async Task The_system_user_exists_and_can_never_sign_in()
    {
        await using var db = fixture.CreateContext();
        var system = await db.Users.SingleAsync(u => u.Id == User.SystemUserId);

        Assert.Equal("system", system.Username);
        Assert.Equal(UserStatus.Suspended, system.Status);
        Assert.False(new BcryptPasswordHasher().Verify("", system.PasswordHash));
        Assert.False(new BcryptPasswordHasher().Verify("!", system.PasswordHash));
    }

    [Fact]
    public async Task A_user_id_with_no_user_behind_it_is_refused_by_the_database()
    {
        await using (var ok = fixture.CreateContext())
        {
            // 0 = the system, 1 = the seeded admin: both real users.
            ok.Currencies.Add(new Currency { Code = $"Q{Random.Shared.Next(10, 99)}", NameAr = "عملة", NameEn = "Currency", CreatedBy = 0, UpdatedBy = 1 });
            await ok.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        db.Currencies.Add(new Currency { Code = $"X{Random.Shared.Next(10, 99)}", NameAr = "عملة", NameEn = "Currency", CreatedBy = 987_654 });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("FK_Currencies_Users_CreatedBy", ex.InnerException!.Message);
    }

    [Fact]
    public async Task Maintenance_purges_expired_keys_and_archives_audit_entries_past_each_companys_retention()
    {
        var now = new DateTime(2026, 9, 19, 3, 0, 0, DateTimeKind.Utc);
        var shortRetention = Random.Shared.NextInt64(1_000_000, 9_000_000);
        var defaultRetention = shortRetention + 1; // no SystemSettings row → 7 years
        var marker = $"Phase3-{Guid.NewGuid():N}";

        await using (var db = fixture.CreateContext())
        {
            db.ProcessedIdempotencyKeys.AddRange(
                new ProcessedIdempotencyKey { IdempotencyKey = Guid.NewGuid(), OperationType = marker, ResultJson = "{}", ExpiresAtUtc = now.AddDays(-1) },
                new ProcessedIdempotencyKey { IdempotencyKey = Guid.NewGuid(), OperationType = marker, ResultJson = "{}", ExpiresAtUtc = now.AddDays(3) });
            db.SystemSettingsRows.Add(new SystemSettings { CompanyId = shortRetention, AuditRetentionYears = 1 });

            AuditLog Entry(long company, DateTime at) => new()
            {
                CompanyId = company, UserId = 0, ActionType = AuditActionType.Update, EntityType = marker, OccurredAtUtc = at
            };
            db.AuditLogs.AddRange(
                Entry(shortRetention, now.AddYears(-2)),     // past 1 year → archived
                Entry(shortRetention, now.AddMonths(-6)),    // inside → stays
                Entry(defaultRetention, now.AddYears(-2)),   // inside the default 7 → stays
                Entry(defaultRetention, now.AddYears(-8)));  // past 7 → archived
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext())
        {
            var result = await new MaintenanceJobs(db).RunAsync(now, default);
            Assert.True(result.IdempotencyKeysPurged >= 1);
            Assert.True(result.AuditEntriesArchived >= 2);
        }

        await using var check = fixture.CreateContext();
        Assert.Equal(1, await check.ProcessedIdempotencyKeys.IgnoreQueryFilters().CountAsync(k => k.OperationType == marker));

        var live = await check.AuditLogs.Where(a => a.EntityType == marker).OrderBy(a => a.OccurredAtUtc).ToListAsync();
        var archived = await check.AuditLogArchives.Where(a => a.EntityType == marker).ToListAsync();
        Assert.Equal(2, live.Count);
        Assert.Equal(2, archived.Count);
        Assert.Contains(archived, a => a.CompanyId == shortRetention && a.OccurredAtUtc == now.AddYears(-2));
        Assert.Contains(archived, a => a.CompanyId == defaultRetention && a.OccurredAtUtc == now.AddYears(-8));
        Assert.All(archived, a => Assert.Equal(now, a.ArchivedAtUtc));

        // The archive is as append-only as the log.
        archived[0].EntityType = "Tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => check.SaveChangesAsync());
    }
}
