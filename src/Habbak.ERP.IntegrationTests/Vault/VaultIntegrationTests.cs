using Xunit;

namespace Habbak.ERP.IntegrationTests.Vault;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §0.6 — proves the VaultSharp client wiring itself (connect,
/// write, read) against a real local dev-mode Vault. Skips (not fails) when none is running, so
/// CI/CD without Vault stays green (rule: Test Skip Logic).
/// </summary>
public class VaultIntegrationTests
{
    [SkippableFact]
    public async Task Connects_to_a_reachable_dev_Vault()
    {
        await VaultTestEnvironment.SkipUnlessReachableAsync();

        var client = VaultTestEnvironment.CreateClient();
        var health = await client.V1.System.GetHealthStatusAsync();

        Assert.False(health.Sealed);
        Assert.True(health.Initialized);
    }

    [SkippableFact]
    public async Task Writes_and_reads_back_a_secret()
    {
        await VaultTestEnvironment.SkipUnlessReachableAsync();

        var client = VaultTestEnvironment.CreateClient();
        var path = $"habbak/test/{Guid.NewGuid():N}";

        await client.V1.Secrets.KeyValue.V2.WriteSecretAsync(
            path: path, data: new Dictionary<string, object> { ["value"] = "round-trip-ok" }, mountPoint: VaultTestEnvironment.MountPath);

        var read = await client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path: path, mountPoint: VaultTestEnvironment.MountPath);

        Assert.Equal("round-trip-ok", read.Data.Data["value"]?.ToString());
    }
}
