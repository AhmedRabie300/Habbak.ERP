using System.Net;

namespace Habbak.ERP.ApiTests.Health;

/// <summary>Docs/Implementation/HR-Core-Plan.md §0.6 — GET /health/vault, unauthenticated like any health probe.</summary>
public class VaultHealthTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    [Fact]
    public async Task Reports_healthy_when_Vault_is_reachable_and_unsealed()
    {
        // The test host's Development config points at a local dev-mode Vault
        // (`vault server -dev -dev-root-token-id=dev-root-token`, Docs/Setup/Vault-Setup.md) — this
        // test only proves the endpoint returns 200 with that Vault reachable, not startup wiring.
        var response = await factory.CreateClient().GetAsync("/health/vault");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }
}
