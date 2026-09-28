using Microsoft.Extensions.Diagnostics.HealthChecks;
using VaultSharp;

namespace Habbak.ERP.API.HealthChecks;

/// <summary>Docs/Implementation/HR-Core-Plan.md §0.6 — GET /health/vault: unsealed and reachable, sealed, or unreachable.</summary>
public sealed class VaultHealthCheck(IVaultClient vaultClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await vaultClient.V1.System.GetHealthStatusAsync();
            return health.Sealed
                ? HealthCheckResult.Unhealthy("Vault is sealed.")
                : HealthCheckResult.Healthy("Vault is initialized and unsealed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Vault is unreachable.", ex);
        }
    }
}
