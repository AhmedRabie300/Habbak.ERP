using Microsoft.Extensions.Configuration;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using SkipException = Xunit.SkipException;

namespace Habbak.ERP.IntegrationTests.Vault;

/// <summary>
/// Shared setup for tests against a local dev-mode Vault (Docs/Setup/Vault-Setup.md:
/// `vault server -dev -dev-root-token-id=dev-root-token`, matching appsettings.Development.json).
/// Tests that need a reachable Vault call <see cref="SkipUnlessReachableAsync"/> first — CI/CD
/// without Vault running skips them instead of failing the whole suite (rule, Docs/Implementation/HR-Core-Plan.md §0.6).
/// </summary>
internal static class VaultTestEnvironment
{
    public const string Address = "http://127.0.0.1:8200";
    public const string Token = "dev-root-token";
    public const string MountPath = "secret";

    public static IVaultClient CreateClient(string? address = null, string? token = null) =>
        new VaultClient(new VaultClientSettings(address ?? Address, new TokenAuthMethodInfo(token ?? Token)));

    public static IConfiguration CreateConfiguration(string? address = null, string? token = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Vault:Address"] = address ?? Address,
            ["Vault:Token"] = token ?? Token,
            ["Vault:MountPath"] = MountPath,
            ["Vault:PiiHmacPath"] = "habbak/pii/hmac-key"
        }).Build();

    /// <summary>Throws SkipException (Xunit.SkippableFact) when no Vault answers at <see cref="Address"/>.</summary>
    public static async Task SkipUnlessReachableAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await http.GetAsync($"{Address}/v1/sys/health");
            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 429) // 429 = standby, still "up"
            {
                throw new SkipException($"Vault answered with {response.StatusCode} at {Address}.");
            }
        }
        catch (Exception ex) when (ex is not SkipException)
        {
            throw new SkipException(
                $"No Vault reachable at {Address} — start `vault server -dev -dev-root-token-id={Token}` to run this test ({ex.Message}).");
        }
    }
}
