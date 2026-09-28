using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Persistence;
using Habbak.ERP.Infrastructure.Persistence.Interceptors;
using Habbak.ERP.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Habbak.ERP.Infrastructure.Services;

public sealed record MaintenanceResult(int IdempotencyKeysPurged, int AuditEntriesArchived);

/// <summary>
/// The housekeeping nothing else does, run daily by <see cref="MaintenanceHostedService"/>:
/// <list type="bullet">
/// <item>Expired idempotency keys (00-Project-Overview.md, section 14.1: kept 7 days) are deleted —
/// they only exist to recognise a retry, and a retry a week later is not one.</item>
/// <item>Audit entries older than their company's AuditRetentionYears move to AuditLogsArchive, in
/// one transaction per company. The only code allowed to take rows out of the append-only log, and
/// it does so by moving them, never by deleting them.</item>
/// </list>
/// </summary>
public sealed class MaintenanceJobs(AppDbContext db)
{
    public const int DefaultRetentionYears = 7;

    public async Task<MaintenanceResult> RunAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var purged = await db.ProcessedIdempotencyKeys.IgnoreQueryFilters()
            .Where(k => k.ExpiresAtUtc < utcNow)
            .ExecuteDeleteAsync(cancellationToken);

        return new MaintenanceResult(purged, await ArchiveAuditLogAsync(utcNow, cancellationToken));
    }

    private async Task<int> ArchiveAuditLogAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var retention = await db.SystemSettingsRows.IgnoreQueryFilters()
            .Where(s => !s.IsDeleted)
            .ToDictionaryAsync(s => s.CompanyId!.Value, s => s.AuditRetentionYears, cancellationToken);

        var companies = await db.AuditLogs.Select(a => a.CompanyId).Distinct().ToListAsync(cancellationToken);
        var archived = 0;

        foreach (var companyId in companies)
        {
            var years = companyId is not null && retention.TryGetValue(companyId.Value, out var y) ? y : DefaultRetentionYears;
            var cutoff = utcNow.AddYears(-Math.Max(1, years));

            // -1 stands for "no company" so one parameterised statement serves both cases.
            var company = companyId ?? -1L;

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            archived += await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO AuditLogsArchive (Id, CompanyId, BranchId, UserId, ActionType, EntityType, EntityId, FieldName, OldValue, NewValue,
    IpAddress, UserAgent, OccurredAtUtc, AdditionalData, ArchivedAtUtc)
SELECT Id, CompanyId, BranchId, UserId, ActionType, EntityType, EntityId, FieldName, OldValue, NewValue,
    IpAddress, UserAgent, OccurredAtUtc, AdditionalData, {utcNow}
FROM AuditLogs
WHERE ISNULL(CompanyId, -1) = {company} AND OccurredAtUtc < {cutoff}", cancellationToken);

            // Only rows now safely in the archive leave the log.
            await db.Database.ExecuteSqlInterpolatedAsync($@"
DELETE l FROM AuditLogs l
WHERE ISNULL(l.CompanyId, -1) = {company} AND l.OccurredAtUtc < {cutoff}
  AND EXISTS (SELECT 1 FROM AuditLogsArchive a WHERE a.Id = l.Id)", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return archived;
    }
}

/// <summary>
/// Runs <see cref="MaintenanceJobs"/> a few minutes after start-up and then once a day. Off when
/// configuration says "Maintenance:Enabled" = false (the API test hosts). Uses its own context with
/// the system as the acting user, like the start-up seeder — there is no request to take one from.
/// </summary>
public sealed class MaintenanceHostedService(IConfiguration configuration, IServiceScopeFactory scopes, ILogger<MaintenanceHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(configuration["Maintenance:Enabled"], "false", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(configuration.GetConnectionString("Default"))
                .AddInterceptors(new AuditSaveChangesInterceptor(new SystemCurrentCompanyContext()))
                .Options;
            await using var db = new AppDbContext(options, new SystemCurrentCompanyContext());
            var result = await new MaintenanceJobs(db).RunAsync(DateTime.UtcNow, cancellationToken);
            logger.LogInformation(
                "Maintenance: purged {Keys} expired idempotency keys, archived {Entries} audit entries.",
                result.IdempotencyKeysPurged, result.AuditEntriesArchived);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Housekeeping must never take the application down; it tries again tomorrow.
            logger.LogError(ex, "Maintenance run failed.");
        }

        // The fixed-assets jobs keep their own per-company error handling.
        await new FixedAssetJobs(scopes, logger).RunAsync(DateOnly.FromDateTime(DateTime.Now), cancellationToken);
    }
}
