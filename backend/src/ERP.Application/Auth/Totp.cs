using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Auth;

/// <summary>
/// Time-based one-time passwords (RFC 6238 over RFC 4226): HMAC-SHA1, 30-second steps, 6 digits, base32 secrets, which is what
/// Google Authenticator, Microsoft Authenticator, Authy and 1Password expect from an otpauth://totp/ link. The time is passed in
/// (from <see cref="Abstractions.IClock"/>) so drift, replay and expiry can be tested without waiting.
/// </summary>
public static class Totp
{
    public const int Digits = 6;
    public const int PeriodSeconds = 30;
    public const string Algorithm = "SHA1";
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>The 30-second step a UTC instant falls in (seconds since the Unix epoch / 30).</summary>
    public static long StepAt(DateTime utc) => (long)Math.Floor((DateTime.SpecifyKind(utc, DateTimeKind.Utc) - DateTime.UnixEpoch).TotalSeconds / PeriodSeconds);

    /// <summary>The 6-digit code for a step.</summary>
    public static string Code(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--) { counter[i] = (byte)(step & 0xFF); step >>= 8; }
        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(key, counter, hash);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    /// <summary>The code for the step a UTC instant falls in.</summary>
    public static string CodeAt(byte[] key, DateTime utc) => Code(key, StepAt(utc));

    /// <summary>
    /// Checks a code against the current step and <paramref name="drift"/> steps either side (clock skew on the phone). Returns the
    /// matched step, or null when the code is wrong or its step is not newer than <paramref name="lastUsedStep"/> (a replay).
    /// The caller stores the returned step as the new last-used step.
    /// </summary>
    public static long? Validate(byte[] key, string? code, DateTime utcNow, long? lastUsedStep, int drift = 1)
    {
        var clean = Normalise(code);
        if (clean is null) return null;
        var now = StepAt(utcNow);
        for (var d = -drift; d <= drift; d++)
        {
            var step = now + d;
            if (lastUsedStep is { } last && step <= last) continue;
            if (FixedTimeEquals(Code(key, step), clean)) return step;
        }
        return null;
    }

    /// <summary>Digits only, with the spaces and dashes people type removed; null unless exactly six digits remain.</summary>
    public static string? Normalise(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var clean = new string(code.Where(c => c is not (' ' or '-')).ToArray());
        return clean.Length == Digits && clean.All(char.IsAsciiDigit) ? clean : null;
    }

    /// <summary>Decodes an RFC 4648 base32 secret (case-insensitive; spaces and padding ignored).</summary>
    public static byte[] DecodeBase32(string base32)
    {
        var clean = base32.Replace(" ", "").TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(clean.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in clean)
        {
            var v = Base32Alphabet.IndexOf(c);
            if (v < 0) throw new FormatException("The secret is not valid base32.");
            buffer = (buffer << 5) | v;
            bits += 5;
            if (bits >= 8) { bits -= 8; bytes.Add((byte)((buffer >> bits) & 0xFF)); }
        }
        return bytes.ToArray();
    }

    /// <summary>The secret in groups of four for typing into an app by hand ("ABCD EFGH ...").</summary>
    public static string FormatSecret(string base32)
    {
        var clean = base32.Replace(" ", "");
        return string.Join(' ', Enumerable.Range(0, (clean.Length + 3) / 4).Select(i => clean.Substring(i * 4, Math.Min(4, clean.Length - i * 4))));
    }

    /// <summary>The standard key-URI an authenticator app scans: otpauth://totp/Issuer:account?secret=...&amp;issuer=Issuer&amp;digits=6&amp;period=30.</summary>
    public static string OtpAuthUri(string issuer, string account, string base32Secret)
    {
        var i = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{i}:{Uri.EscapeDataString(account)}?secret={base32Secret.Replace(" ", "")}&issuer={i}&digits={Digits}&period={PeriodSeconds}";
    }

    private static bool FixedTimeEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
}
