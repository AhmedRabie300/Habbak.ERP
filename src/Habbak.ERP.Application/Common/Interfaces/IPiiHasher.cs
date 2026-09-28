namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>
/// HMAC-SHA256 over a PII value (e.g. the future Employee.NationalIdHash) for uniqueness/lookup
/// without ever storing the value itself in the clear — never used to reverse anything, unlike
/// <see cref="ISecretProtector"/>. The key material lives in Vault, never in the code or the
/// database (Docs/Implementation/HR-Core-Plan.md §0.6).
/// </summary>
public interface IPiiHasher
{
    /// <summary>The hash to store for a value written just now — always under the current key.</summary>
    Task<string> ComputeHashAsync(string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// The hash(es) to compare a candidate value against already-stored hashes with: the current
    /// key's hash, plus the previous key's (when one exists) so a lookup or uniqueness check still
    /// matches rows hashed before the key last rotated.
    /// </summary>
    Task<IReadOnlyList<string>> ComputeHashCandidatesAsync(string value, CancellationToken cancellationToken = default);
}
