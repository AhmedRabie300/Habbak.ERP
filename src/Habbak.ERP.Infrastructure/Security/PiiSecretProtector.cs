using System.Security.Cryptography;
using Habbak.ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Habbak.ERP.Infrastructure.Security;

/// <summary>
/// ISecretProtector for column-level PII encryption (Docs/Implementation/HR-Core-Plan.md §0.3),
/// registered as the keyed singleton "HR.PII" — a separate Data Protection purpose
/// ("Habbak.ERP.HR.PII.v1") from the 2FA secrets protector (API/Auth/DataProtectionSecretProtector.cs,
/// "Habbak.ERP.Secrets.v1"), so rotating one key ring never touches the other. Lives in Infrastructure
/// rather than mirroring that class directly: Infrastructure cannot reference the API project.
/// ProtectFor/UnprotectTimed are implemented for interface completeness but unused by
/// EncryptedStringConverter, which only ever calls Protect/Unprotect.
/// </summary>
public sealed class PiiSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _pii = provider.CreateProtector("Habbak.ERP.HR.PII.v1");
    private readonly ITimeLimitedDataProtector _piiTimed = provider.CreateProtector("Habbak.ERP.HR.PII.v1").ToTimeLimitedDataProtector();

    public string Protect(string plaintext) => _pii.Protect(plaintext);

    public string? Unprotect(string protectedValue)
    {
        try
        {
            return _pii.Unprotect(protectedValue);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public string ProtectFor(string plaintext, TimeSpan lifetime) => _piiTimed.Protect(plaintext, lifetime);

    public string? UnprotectTimed(string token)
    {
        try
        {
            return _piiTimed.Unprotect(token);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
