using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.FixedAssets;
using Habbak.ERP.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Habbak.ERP.Infrastructure.Services;

public sealed record FixedAssetJobResult(long CompanyId, string Depreciation, int MaintenanceRequestsRaised, string? Error);

/// <summary>
/// The two daily jobs of 08-Module-Maintenance-FixedAssets (section 3), run for every active company
/// in its own DI scope under <see cref="BackgroundCompanyScope"/> — the same commands a user would
/// send, so every rule (and the idempotency of rules 28-29) holds for the job too:
/// <list type="bullet">
/// <item>RunDueDepreciation — the month's run, created or found by its (company, year, month) key, and posted.</item>
/// <item>GenerateDueMaintenanceRequests — a request per due schedule occurrence, keyed by (schedule, due date).</item>
/// </list>
/// A company that fails is logged and skipped; the others still run.
/// </summary>
public sealed class FixedAssetJobs(IServiceScopeFactory scopes, ILogger logger)
{
    public async Task<IReadOnlyList<FixedAssetJobResult>> RunAsync(DateOnly today, CancellationToken cancellationToken)
    {
        List<long> companies;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            companies = await db.Companies.IgnoreQueryFilters().Where(c => !c.IsDeleted && c.IsActive).Select(c => c.Id).ToListAsync(cancellationToken);
        }

        var results = new List<FixedAssetJobResult>();
        foreach (var companyId in companies)
        {
            results.Add(await RunForCompanyAsync(companyId, today, cancellationToken));
        }

        return results;
    }

    public async Task<FixedAssetJobResult> RunForCompanyAsync(long companyId, DateOnly today, CancellationToken cancellationToken)
    {
        using var company = BackgroundCompanyScope.Begin(companyId);
        var depreciation = "skipped";
        var raised = 0;
        try
        {
            // One scope per job: a failed depreciation run leaves nothing half-tracked for the maintenance job.
            await using (var scope = scopes.CreateAsyncScope())
            {
                depreciation = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunDueDepreciationCommand(today), cancellationToken);
            }

            await using (var scope = scopes.CreateAsyncScope())
            {
                raised = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GenerateDueMaintenanceRequestsCommand(today), cancellationToken);
            }

            if (depreciation == "posted" || raised > 0)
            {
                logger.LogInformation(
                    "Fixed assets jobs for company {Company}: depreciation {Depreciation}, {Raised} maintenance requests raised.",
                    companyId, depreciation, raised);
            }

            return new FixedAssetJobResult(companyId, depreciation, raised, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Fixed assets jobs failed for company {Company}.", companyId);
            return new FixedAssetJobResult(companyId, depreciation, raised, ex.Message);
        }
    }
}
