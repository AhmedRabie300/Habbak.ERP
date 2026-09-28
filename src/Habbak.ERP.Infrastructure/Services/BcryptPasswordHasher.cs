using Habbak.ERP.Application.Common.Interfaces;

namespace Habbak.ERP.Infrastructure.Services;

/// <summary>BCrypt with a work factor of 12. The salt is generated per hash and stored inside it.</summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // A stored value that is not a BCrypt hash never matches.
            return false;
        }
    }
}
