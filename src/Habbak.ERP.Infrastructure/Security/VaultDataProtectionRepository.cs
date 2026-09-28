using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using VaultSharp;
using VaultSharp.Core;

namespace Habbak.ERP.Infrastructure.Security;

/// <summary>
/// Stores the ASP.NET Core Data Protection key ring's XML in Vault instead of the local filesystem
/// (Docs/Implementation/HR-Core-Plan.md §0.6) — losing the API's App_Data folder no longer means
/// losing every 2FA secret ever protected with it. IXmlRepository is a synchronous contract (ASP.NET
/// Core calls it during startup, before any async pipeline exists), so the VaultSharp calls below are
/// awaited synchronously — the same constraint every custom IXmlRepository (Redis, Blob Storage...)
/// has to work within.
/// </summary>
public sealed class VaultDataProtectionRepository : IXmlRepository
{
    private readonly IVaultClient _vaultClient;
    private readonly string _mountPath;
    private readonly string _basePath;

    public VaultDataProtectionRepository(IVaultClient vaultClient, IConfiguration configuration)
    {
        _vaultClient = vaultClient;
        _mountPath = configuration["Vault:MountPath"] ?? "secret";
        _basePath = configuration["Vault:DataProtectionKeysPath"] ?? "habbak/dataprotection-keys";
    }

    public IReadOnlyCollection<XElement> GetAllElements() =>
        GetAllElementsAsync().GetAwaiter().GetResult();

    private async Task<IReadOnlyCollection<XElement>> GetAllElementsAsync()
    {
        List<string> names;
        try
        {
            var listed = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretPathsAsync(_basePath, mountPoint: _mountPath);
            names = listed.Data.Keys.ToList();
        }
        catch (VaultApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return []; // No keys stored yet — the very first run.
        }

        var elements = new List<XElement>();
        foreach (var name in names)
        {
            var secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync($"{_basePath}/{name}", mountPoint: _mountPath);
            if (secret.Data.Data.TryGetValue("xml", out var xmlObj) && xmlObj?.ToString() is { } xml)
            {
                elements.Add(XElement.Parse(xml));
            }
        }

        return elements;
    }

    public void StoreElement(XElement element, string friendlyName) =>
        StoreElementAsync(element, friendlyName).GetAwaiter().GetResult();

    private async Task StoreElementAsync(XElement element, string friendlyName)
    {
        var safeName = Uri.EscapeDataString(friendlyName);
        await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(
            path: $"{_basePath}/{safeName}",
            data: new Dictionary<string, object> { ["xml"] = element.ToString(SaveOptions.DisableFormatting) },
            mountPoint: _mountPath);
    }
}
