using System.Security.Cryptography;
using Habbak.ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Habbak.ERP.API.Auth;

/// <summary>
/// ISecretProtector over ASP.NET Data Protection. The keys are kept on disk (Program.cs,
/// "DataProtection:KeysPath") so encrypted authenticator secrets survive a restart; losing the keys
/// means every user with two-factor sign-in needs an administrator reset.
/// </summary>
public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _secrets = provider.CreateProtector("Habbak.ERP.Secrets.v1");
    private readonly ITimeLimitedDataProtector _tokens = provider.CreateProtector("Habbak.ERP.Tokens.v1").ToTimeLimitedDataProtector();

    public string Protect(string plaintext) => _secrets.Protect(plaintext);

    public string? Unprotect(string protectedValue)
    {
        try
        {
            return _secrets.Unprotect(protectedValue);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public string ProtectFor(string plaintext, TimeSpan lifetime) => _tokens.Protect(plaintext, lifetime);

    public string? UnprotectTimed(string token)
    {
        try
        {
            return _tokens.Unprotect(token);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
