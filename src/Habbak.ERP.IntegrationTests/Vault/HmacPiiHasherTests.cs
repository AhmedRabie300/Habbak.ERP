using System.Security.Cryptography;
using System.Text;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Habbak.ERP.IntegrationTests.Vault;

/// <summary>Docs/Implementation/HR-Core-Plan.md §0.6 — HmacPiiHasher against a real local dev-mode Vault.</summary>
public class HmacPiiHasherTests
{
    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Habbak.ERP.IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static HmacPiiHasher CreateHasher(string? vaultAddress = null, string environmentName = "Development") =>
        new(VaultTestEnvironment.CreateClient(address: vaultAddress),
            VaultTestEnvironment.CreateConfiguration(address: vaultAddress),
            new StubEnvironment(environmentName));

    [SkippableFact]
    public async Task Reads_the_HMAC_key_from_Vault_and_hashes_deterministically()
    {
        await VaultTestEnvironment.SkipUnlessReachableAsync();
        var hasher = CreateHasher();

        var first = await hasher.ComputeHashAsync("29001010112345");
        var second = await hasher.ComputeHashAsync("29001010112345");

        Assert.Equal(first, second);
        Assert.NotEmpty(first);
    }

    [SkippableFact]
    public async Task Different_values_hash_differently()
    {
        await VaultTestEnvironment.SkipUnlessReachableAsync();
        var hasher = CreateHasher();

        var a = await hasher.ComputeHashAsync("29001010112345");
        var b = await hasher.ComputeHashAsync("29001010112346");

        Assert.NotEqual(a, b);
    }

    [SkippableFact]
    public async Task ComputeHashCandidates_includes_the_previous_key_when_one_is_set()
    {
        await VaultTestEnvironment.SkipUnlessReachableAsync();
        var client = VaultTestEnvironment.CreateClient();
        await client.V1.Secrets.KeyValue.V2.WriteSecretAsync(
            path: "habbak/pii/hmac-key", mountPoint: VaultTestEnvironment.MountPath,
            data: new Dictionary<string, object> { ["current"] = "current-test-key", ["previous"] = "previous-test-key" });

        var hasher = CreateHasher();
        var candidates = await hasher.ComputeHashCandidatesAsync("29001010112345");

        var expectedCurrent = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes("current-test-key"), Encoding.UTF8.GetBytes("29001010112345"))).ToLowerInvariant();
        var expectedPrevious = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes("previous-test-key"), Encoding.UTF8.GetBytes("29001010112345"))).ToLowerInvariant();

        Assert.Equal(2, candidates.Count);
        Assert.Contains(expectedCurrent, candidates);
        Assert.Contains(expectedPrevious, candidates);

        // Restore the real dev key so other tests in this run are unaffected.
        await client.V1.Secrets.KeyValue.V2.WriteSecretAsync(
            path: "habbak/pii/hmac-key", mountPoint: VaultTestEnvironment.MountPath,
            data: new Dictionary<string, object> { ["current"] = "a761e91cfbbf06a6cd098a9a5e50e40fe7b79b643562168493f5101e4ac85436", ["previous"] = "" });
    }

    [Fact]
    public async Task Falls_back_to_the_environment_variable_in_Development_when_Vault_is_unreachable()
    {
        Environment.SetEnvironmentVariable("HABBAK_PII_HMAC_KEY", "fallback-test-key");
        try
        {
            // Port 1 refuses connections instantly — no real Vault involved in this test at all.
            var hasher = CreateHasher(vaultAddress: "http://127.0.0.1:1");

            var hash = await hasher.ComputeHashAsync("29001010112345");

            var expected = Convert.ToHexString(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes("fallback-test-key"), Encoding.UTF8.GetBytes("29001010112345"))).ToLowerInvariant();
            Assert.Equal(expected, hash);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HABBAK_PII_HMAC_KEY", null);
        }
    }

    [Fact]
    public async Task Rethrows_outside_Development_instead_of_falling_back()
    {
        Environment.SetEnvironmentVariable("HABBAK_PII_HMAC_KEY", "fallback-test-key");
        try
        {
            var hasher = CreateHasher(vaultAddress: "http://127.0.0.1:1", environmentName: Environments.Production);

            await Assert.ThrowsAnyAsync<Exception>(() => hasher.ComputeHashAsync("29001010112345"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HABBAK_PII_HMAC_KEY", null);
        }
    }
}
