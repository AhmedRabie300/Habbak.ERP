using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace Habbak.ERP.Infrastructure.Security;

/// <summary>
/// Wires an <see cref="IVaultClient"/> from configuration (Docs/Implementation/HR-Core-Plan.md §0.6).
/// Production must supply "Vault:Address" and "Vault:Token" (an environment variable or a secrets
/// store, never appsettings.json itself) — a missing value throws immediately rather than silently
/// picking a default. In Development only, a missing value falls back to the local
/// `vault server -dev` a developer runs by hand (Docs/Setup/Vault-Setup.md). This is a separate
/// concern from <see cref="Services.HmacPiiHasher"/>'s own Development fallback: this method covers
/// "no Vault:Address configured at all", HmacPiiHasher covers "Vault is configured but currently
/// unreachable" (connection refused) even when a real address is set.
/// </summary>
public static class VaultServiceCollectionExtensions
{
    public static IServiceCollection AddVaultClient(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton<IVaultClient>(_ =>
        {
            var vault = configuration.GetSection("Vault");
            var address = vault["Address"];
            var token = vault["Token"];

            if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(token))
            {
                if (environment.IsDevelopment())
                {
                    // Matches the local `vault server -dev` a developer runs by hand
                    // (Docs/Setup/Vault-Setup.md) — never used outside Development.
                    // `??=` only replaces a null value: appsettings.json ships "Vault:Token": ""
                    // (an empty string, not null, so callers never accidentally commit a real
                    // token there) which `??=` would leave empty, so TokenAuthMethodInfo below
                    // throws instead of falling back. IsNullOrWhiteSpace catches that case too.
                    if (string.IsNullOrWhiteSpace(address))
                    {
                        address = "http://127.0.0.1:8200";
                    }

                    if (string.IsNullOrWhiteSpace(token))
                    {
                        token = "dev-root-token";
                    }
                }
                else
                {
                    throw new InvalidOperationException(
                        "Vault:Address and Vault:Token are required outside Development (Docs/Implementation/HR-Core-Plan.md §0.6) — set them via environment variables or a secrets store, never in appsettings.json.");
                }
            }

            var authMethod = new TokenAuthMethodInfo(token);
            var settings = new VaultClientSettings(address, authMethod);

            if (bool.TryParse(vault["SkipTlsVerify"], out var skipTlsVerify) && skipTlsVerify)
            {
                // Self-signed certificates only (Docs/Setup/Vault-Setup.md) — never set outside a
                // local/dev-mode Vault; production terminates TLS with a real certificate.
                settings.PostProcessHttpClientHandlerAction = handler =>
                {
                    if (handler is HttpClientHandler clientHandler)
                    {
                        clientHandler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                    }
                };
            }

            return new VaultClient(settings);
        });

        return services;
    }
}
