using Habbak.ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Habbak.ERP.Infrastructure.Persistence.Converters;

/// <summary>
/// EF Core column-level encryption (Docs/Implementation/HR-Core-Plan.md §0.3) — the column is never
/// stored as plain text, encrypted with the "HR.PII"-keyed <see cref="ISecretProtector"/>
/// (<see cref="Security.PiiSecretProtector"/>, purpose "Habbak.ERP.HR.PII.v1"), a separate key ring
/// from 2FA secrets. Null values are never passed through the conversion at all — EF Core stores a
/// null column as null without calling either side of the converter — so no null handling is needed
/// here. Not yet applied to any entity (Phase 1 wires it onto EmployeePersonalData.NationalIdEncrypted
/// / BankIbanEncrypted — this is the converter itself, tested against a throwaway entity).
/// </summary>
public sealed class EncryptedStringConverter : ValueConverter<string, string>
{
    public EncryptedStringConverter(ISecretProtector protector)
        : base(
            plaintext => protector.Protect(plaintext),
            stored => Decrypt(protector, stored))
    {
    }

    private static string Decrypt(ISecretProtector protector, string stored) =>
        protector.Unprotect(stored) ?? throw new InvalidOperationException(
            "Could not decrypt an encrypted column value — the encryption key may be missing, rotated without the old key kept around, or the stored value is corrupted.");
}
