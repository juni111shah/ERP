using System.Text;
using ERP.Application.Abstractions;
using ERP.Application.Auth;
using ERP.Infrastructure.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace ERP.UnitTests;

/// <summary>RFC 6238 TOTP: published test vectors, drift window, replay, parsing and the otpauth:// link.</summary>
public class TotpTests
{
    private static readonly byte[] RfcKey = Encoding.ASCII.GetBytes("12345678901234567890"); // RFC 6238 appendix B, SHA-1
    private const string RfcKeyBase32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    private static DateTime At(long unixSeconds) => DateTime.UnixEpoch.AddSeconds(unixSeconds);

    [Theory]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1111111111, "050471")]
    [InlineData(1234567890, "005924")]
    [InlineData(2000000000, "279037")]
    [InlineData(20000000000, "353130")]
    public void Codes_match_the_RFC_6238_test_vectors(long unixSeconds, string expected)
        => Assert.Equal(expected, Totp.CodeAt(RfcKey, At(unixSeconds)));

    [Fact]
    public void Base32_secrets_decode_case_and_space_insensitively()
    {
        Assert.Equal(RfcKey, Totp.DecodeBase32(RfcKeyBase32));
        Assert.Equal(RfcKey, Totp.DecodeBase32("gezd gnbv gy3t qojq gezd gnbv gy3t qojq"));
        Assert.Throws<FormatException>(() => Totp.DecodeBase32("NOT-BASE32!"));
    }

    [Fact]
    public void The_current_step_and_one_step_either_side_are_accepted()
    {
        var now = At(1_700_000_015); // 15 s into a step
        var step = Totp.StepAt(now);
        Assert.Equal(step, Totp.Validate(RfcKey, Totp.Code(RfcKey, step), now, null));
        Assert.Equal(step - 1, Totp.Validate(RfcKey, Totp.Code(RfcKey, step - 1), now, null));
        Assert.Equal(step + 1, Totp.Validate(RfcKey, Totp.Code(RfcKey, step + 1), now, null));
        Assert.Null(Totp.Validate(RfcKey, Totp.Code(RfcKey, step - 2), now, null));
        Assert.Null(Totp.Validate(RfcKey, Totp.Code(RfcKey, step + 2), now, null));
    }

    [Fact]
    public void A_code_at_or_before_the_last_used_step_is_a_replay()
    {
        var now = At(1_700_000_015);
        var step = Totp.StepAt(now);
        var code = Totp.Code(RfcKey, step);
        Assert.Null(Totp.Validate(RfcKey, code, now, lastUsedStep: step));
        Assert.Null(Totp.Validate(RfcKey, Totp.Code(RfcKey, step - 1), now, lastUsedStep: step - 1));
        Assert.Equal(step, Totp.Validate(RfcKey, code, now, lastUsedStep: step - 1));
        // 30 s later the next code is fine
        Assert.Equal(step + 1, Totp.Validate(RfcKey, Totp.Code(RfcKey, step + 1), now.AddSeconds(30), lastUsedStep: step));
    }

    [Theory]
    [InlineData("123 456", "123456")]
    [InlineData("123-456", "123456")]
    [InlineData(" 123456 ", "123456")]
    [InlineData("12345", null)]
    [InlineData("1234567", null)]
    [InlineData("12a456", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Codes_are_normalised_to_six_digits(string? input, string? expected) => Assert.Equal(expected, Totp.Normalise(input));

    [Fact]
    public void Wrong_or_malformed_codes_fail()
    {
        var now = At(1_700_000_015);
        Assert.Null(Totp.Validate(RfcKey, "abcdef", now, null));
        Assert.Null(Totp.Validate(RfcKey, null, now, null));
        var right = Totp.CodeAt(RfcKey, now);
        Assert.Null(Totp.Validate(RfcKey, right == "000000" ? "111111" : "000000", now, null));
    }

    [Fact]
    public void The_link_is_the_standard_key_uri_and_the_secret_is_grouped_for_typing()
    {
        Assert.Equal("otpauth://totp/My%20Co:a.b%40x.test?secret=JBSWY3DPEHPK3PXP&issuer=My%20Co&digits=6&period=30",
            Totp.OtpAuthUri("My Co", "a.b@x.test", "JBSW Y3DP EHPK 3PXP"));
        Assert.Equal("JBSW Y3DP EHPK 3PXP", Totp.FormatSecret("JBSWY3DPEHPK3PXP"));
        Assert.Equal("JBSW Y3DP E", Totp.FormatSecret("JBSWY3DPE"));
    }

    [Fact]
    public void The_QR_code_is_a_png_data_uri()
    {
        var uri = new QrCodeRenderer().PngDataUri("otpauth://totp/ERP:a%40b.test?secret=JBSWY3DPEHPK3PXP&issuer=ERP&digits=6&period=30");
        Assert.StartsWith("data:image/png;base64,", uri);
        var png = Convert.FromBase64String(uri["data:image/png;base64,".Length..]);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
    }

    [Fact]
    public void Two_factor_is_enforced_by_default()
    {
        var o = new AuthOptions();
        Assert.True(o.TwoFactor.Enforce);
        Assert.Equal(10, o.TwoFactor.ChallengeMinutes);
        Assert.False(string.IsNullOrWhiteSpace(o.TwoFactor.Issuer));
    }
}

/// <summary>Login challenge tokens: scoped, expiring by <see cref="IClock"/>, tamper-proof and voided by a password change.</summary>
public class TwoFactorChallengeTokenTests
{
    private sealed class MovableClock(DateTime now) : IClock { public DateTime UtcNow { get; set; } = now; }

    private static (TwoFactorChallenges Tokens, MovableClock Clock) Create(int minutes = 10)
    {
        var clock = new MovableClock(new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc));
        var options = Options.Create(new AuthOptions { TwoFactor = new TwoFactorOptions { ChallengeMinutes = minutes } });
        return (new TwoFactorChallenges(new EphemeralDataProtectionProvider(), clock, options), clock);
    }

    [Fact]
    public void A_challenge_round_trips_its_user_purpose_and_remember_me()
    {
        var (tokens, _) = Create();
        var id = Guid.NewGuid();
        var c = tokens.Read(tokens.Issue(id, "hash", rememberMe: true, TwoFactorChallengePurpose.Setup))!;
        Assert.Equal(id, c.UserId);
        Assert.True(c.RememberMe);
        Assert.Equal(TwoFactorChallengePurpose.Setup, c.Purpose);
        Assert.Equal(TwoFactorChallenges.Fingerprint("hash"), c.PasswordFingerprint);
        Assert.NotEqual(TwoFactorChallenges.Fingerprint("other-hash"), c.PasswordFingerprint);
        Assert.Equal(TwoFactorChallengePurpose.Verify, tokens.Read(tokens.Issue(id, "hash", false, TwoFactorChallengePurpose.Verify))!.Purpose);
    }

    [Fact]
    public void A_challenge_expires_after_the_configured_minutes()
    {
        var (tokens, clock) = Create(minutes: 10);
        var token = tokens.Issue(Guid.NewGuid(), "hash", false, TwoFactorChallengePurpose.Setup);
        Assert.Equal(600, tokens.LifetimeSeconds);
        clock.UtcNow = clock.UtcNow.AddMinutes(9).AddSeconds(59);
        Assert.NotNull(tokens.Read(token));
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        Assert.Null(tokens.Read(token));
    }

    [Fact]
    public void Tampered_or_foreign_tokens_are_refused()
    {
        var (tokens, _) = Create();
        var token = tokens.Issue(Guid.NewGuid(), "hash", false, TwoFactorChallengePurpose.Setup);
        Assert.Null(tokens.Read(token[..^4] + (token.EndsWith("AAAA") ? "BBBB" : "AAAA")));
        Assert.Null(tokens.Read("not-a-token"));
        Assert.Null(tokens.Read(""));
        Assert.Null(tokens.Read(null));
        var (other, _) = Create(); // different key ring, like another deployment
        Assert.Null(other.Read(token));
    }
}
