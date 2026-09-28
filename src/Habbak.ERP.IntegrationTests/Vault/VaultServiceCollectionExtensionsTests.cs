using Habbak.ERP.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using VaultSharp;
using Xunit;

namespace Habbak.ERP.IntegrationTests.Vault;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §0.6, rule: "Production Mode إجباري — مفيش Dev Mode في
/// الكود النهائي" — outside Development, Vault:Address/Vault:Token are mandatory, never a silent
/// localhost default.
/// </summary>
public class VaultServiceCollectionExtensionsTests
{
    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Habbak.ERP.IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IVaultClient Build(IConfiguration configuration, string environmentName)
    {
        var services = new ServiceCollection();
        services.AddVaultClient(configuration, new StubEnvironment(environmentName));
        return services.BuildServiceProvider().GetRequiredService<IVaultClient>();
    }

    [Fact]
    public void Throws_outside_Development_when_Vault_configuration_is_missing()
    {
        var configuration = new ConfigurationBuilder().Build(); // no Vault:Address/Vault:Token at all

        Assert.Throws<InvalidOperationException>(() => Build(configuration, Environments.Production));
    }

    [Fact]
    public void Defaults_to_the_local_dev_Vault_in_Development_when_configuration_is_missing()
    {
        var configuration = new ConfigurationBuilder().Build();

        // Building the client does not itself contact Vault — this only proves no exception is
        // thrown for the missing-config case in Development (the dev-mode defaults kick in).
        var client = Build(configuration, Environments.Development);

        Assert.NotNull(client);
    }

    [Fact]
    public void Uses_explicit_configuration_when_present_regardless_of_environment()
    {
        var configuration = VaultTestEnvironment.CreateConfiguration();

        var client = Build(configuration, Environments.Production);

        Assert.NotNull(client);
    }

    /// <summary>
    /// Regression for a real bug found while verifying Phase 3B: appsettings.json ships
    /// "Vault:Token": "" (an empty string, not a missing key, so a real token is never
    /// accidentally committed there) alongside a real "Vault:Address". The old code used
    /// `token ??= "dev-root-token"`, which only replaces a null value — an empty string slipped
    /// through untouched and reached VaultSharp's TokenAuthMethodInfo, which throws
    /// ArgumentException on an empty token instead of the intended dev fallback ever kicking in.
    /// </summary>
    [Fact]
    public void EmptyToken_UsesDevFallback()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Vault:Address"] = "https://vault:8200", // present and non-empty, same as appsettings.json
            ["Vault:Token"] = "" // present but empty, same as appsettings.json — not a missing key
        }).Build();

        var client = Build(configuration, Environments.Development);

        Assert.NotNull(client);
    }
}
