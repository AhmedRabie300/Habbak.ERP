namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>Hashes and checks user passwords. The implementation is BCrypt (Infrastructure).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
