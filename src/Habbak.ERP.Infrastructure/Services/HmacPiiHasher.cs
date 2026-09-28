using System.Security.Cryptography;
using System.Text;
using Habbak.ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using VaultSharp;

namespace Habbak.ERP.Infrastructure.Services;

/// <summary>
/// Reads the HMAC key material from Vault ("Vault:MountPath"/"Vault:PiiHmacPath", e.g.
/// secret/data/habbak/pii/hmac-key) with a one-hour in-memory cache, and computes HMAC-SHA256 over a
/// PII value with it (Docs/Implementation/HR-Core-Plan.md §0.6). Supports a "current" and "previous"
/// key so a value hashed just before a rotation still matches on lookup during the rotation's grace
/// window. In Development only, an unreachable Vault falls back to the HABBAK_PII_HMAC_KEY
/// environment variable — outside Development this rethrows instead, since a silent fallback there
/// would mean PII hashes computed with a key nobody backed up (rule: no Dev Mode in the final code).
/// </summary>
public sealed class HmacPiiHasher(IVaultClient vaultClient, IConfiguration configuration, IHostEnvironment environment) : IPiiHasher
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private (string Current, string? Previous, DateTime ExpiresAtUtc)? _cached;

    private async Task<(string Current, string? Previous)> KeysAsync(CancellationToken cancellationToken)
    {
        if (_cached is { } fresh && fresh.ExpiresAtUtc > DateTime.UtcNow)
        {
            return (fresh.Current, fresh.Previous);
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_cached is { } stillFresh && stillFresh.ExpiresAtUtc > DateTime.UtcNow)
            {
                return (stillFresh.Current, stillFresh.Previous);
            }

            var (current, previous) = await ReadKeysAsync(cancellationToken);
            _cached = (current, previous, DateTime.UtcNow.Add(CacheDuration));
            return (current, previous);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<(string Current, string? Previous)> ReadKeysAsync(CancellationToken cancellationToken)
    {
        try
        {
            var mountPath = configuration["Vault:MountPath"] ?? "secret";
            var path = configuration["Vault:PiiHmacPath"] ?? "habbak/pii/hmac-key";
            var secret = await vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(path: path, mountPoint: mountPath);

            var current = secret.Data.Data.TryGetValue("current", out var currentObj) ? currentObj?.ToString() : null;
            if (string.IsNullOrEmpty(current))
            {
                throw new InvalidOperationException($"Vault secret '{mountPath}/{path}' has no 'current' key.");
            }

            var previous = secret.Data.Data.TryGetValue("previous", out var previousObj) ? previousObj?.ToString() : null;
            return (current, string.IsNullOrEmpty(previous) ? null : previous);
        }
        catch (Exception ex) when (environment.IsDevelopment() && ex is not InvalidOperationException)
        {
            // Vault unreachable (connection refused, not yet started locally) — Development only.
            var fallback = Environment.GetEnvironmentVariable("HABBAK_PII_HMAC_KEY");
            if (string.IsNullOrEmpty(fallback))
            {
                throw new InvalidOperationException(
                    "Vault is unreachable and no HABBAK_PII_HMAC_KEY fallback is set for local Development.", ex);
            }

            return (fallback, null);
        }
    }

    public async Task<string> ComputeHashAsync(string value, CancellationToken cancellationToken = default)
    {
        var (current, _) = await KeysAsync(cancellationToken);
        return Compute(value, current);
    }

    public async Task<IReadOnlyList<string>> ComputeHashCandidatesAsync(string value, CancellationToken cancellationToken = default)
    {
        var (current, previous) = await KeysAsync(cancellationToken);
        return previous is null ? [Compute(value, current)] : [Compute(value, current), Compute(value, previous)];
    }

    private static string Compute(string value, string key) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
