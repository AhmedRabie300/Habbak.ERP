using System.Security.Cryptography;
using System.Text;

namespace Habbak.ERP.Application.Settings.Auth;

/// <summary>
/// Time-based one-time codes (RFC 6238 over RFC 4226, HMAC-SHA1, 30-second steps, 6 digits) — what
/// Google / Microsoft Authenticator and the like show. The secret travels as Base32 (RFC 4648).
/// </summary>
public static class Totp
{
    public const int StepSeconds = 30;
    public const int Digits = 6;

    /// <summary>A code from the step before or after also passes — phones drift and people type slowly.</summary>
    private const int AllowedDrift = 1;

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>160 random bits, the size RFC 4226 recommends for HMAC-SHA1.</summary>
    public static string NewSecret() => ToBase32(RandomNumberGenerator.GetBytes(20));

    public static long StepAt(DateTime utcNow) => new DateTimeOffset(utcNow, TimeSpan.Zero).ToUnixTimeSeconds() / StepSeconds;

    public static string Code(byte[] key, long step, int digits = Digits)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(step & 0xff);
            step >>= 8;
        }

        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        var modulo = (int)Math.Pow(10, digits);
        return (binary % modulo).ToString().PadLeft(digits, '0');
    }

    /// <summary>
    /// The step the code belongs to, or null when it matches none near now — or only a step at or
    /// before <paramref name="lastUsedStep"/>, so a code seen once cannot be used again.
    /// </summary>
    public static long? Verify(string base32Secret, string? code, DateTime utcNow, long? lastUsedStep)
    {
        var digits = new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length != Digits)
        {
            return null;
        }

        var key = FromBase32(base32Secret);
        var now = StepAt(utcNow);
        for (var step = now - AllowedDrift; step <= now + AllowedDrift; step++)
        {
            if ((lastUsedStep is null || step > lastUsedStep)
                && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Code(key, step)), Encoding.ASCII.GetBytes(digits)))
            {
                return step;
            }
        }

        return null;
    }

    /// <summary>The link an authenticator app reads from the QR code (Key Uri Format).</summary>
    public static string SetupUri(string issuer, string accountName, string base32Secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}" +
        $"?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";

    public static string ToBase32(byte[] data)
    {
        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                result.Append(Base32Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            result.Append(Base32Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return result.ToString();
    }

    public static byte[] FromBase32(string text)
    {
        var clean = text.Replace(" ", string.Empty).Replace("-", string.Empty).TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(clean.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in clean)
        {
            var value = Base32Alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new FormatException("Not a Base32 string.");
            }

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)((buffer >> (bits - 8)) & 0xff));
                bits -= 8;
            }
        }

        return bytes.ToArray();
    }
}
