using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Authenticator-app (TOTP) two-factor sign-in with recovery codes.</summary>
public class TwoFactorTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Pw = "Temp#Passw0rd";

    /// <summary>RFC 6238 TOTP (SHA-1, 30 s, 6 digits) from a base32 secret.</summary>
    private static string Totp(string base32, int stepOffset = 0)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = string.Concat(base32.Replace(" ", "").ToUpperInvariant().TrimEnd('=').Select(ch => Convert.ToString(alphabet.IndexOf(ch), 2).PadLeft(5, '0')));
        var key = Enumerable.Range(0, bits.Length / 8).Select(i => Convert.ToByte(bits.Substring(i * 8, 8), 2)).ToArray();
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30 + stepOffset;
        var msg = BitConverter.GetBytes(counter); if (BitConverter.IsLittleEndian) Array.Reverse(msg);
        var hash = HMACSHA1.HashData(key, msg);
        var o = hash[^1] & 0x0f;
        var code = ((hash[o] & 0x7f) << 24 | hash[o + 1] << 16 | hash[o + 2] << 8 | hash[o + 3]) % 1_000_000;
        return code.ToString("D6");
    }

    private async Task<string> NewUserAsync()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var roles = ApiFactory.Unwrap(JsonNode.Parse(await (await admin.GetAsync("/api/v1/roles")).Content.ReadAsStringAsync())!);
        var role = (string)roles.AsArray().First(r => (string?)r!["name"] == "Viewer")!["id"]!;
        var email = $"tf{Guid.NewGuid():N}"[..10] + "@demo.local";
        (await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "TF User", temporaryPassword = Pw, roleIds = new[] { role } })).EnsureSuccessStatusCode();
        return email;
    }

    private static async Task<JsonNode> Body(HttpResponseMessage r, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == expected, $"Expected {expected} but got {r.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    [Fact]
    public async Task Two_factor_is_set_up_enforced_at_sign_in_and_can_be_switched_off()
    {
        var email = await NewUserAsync();
        var (c, _) = await f.SignInAsync(email, Pw);
        Assert.False((bool)(await Body(await c.GetAsync("/api/v1/auth/2fa")))["enabled"]!);

        var setup = await Body(await c.PostAsync("/api/v1/auth/2fa/setup", null));
        var secret = (string)setup["secret"]!;
        Assert.StartsWith("otpauth://totp/", (string)setup["otpAuthUri"]!);

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/auth/2fa/enable", new { code = "000000" })).StatusCode);
        var enabled = await Body(await c.PostAsJsonAsync("/api/v1/auth/2fa/enable", new { code = Totp(secret) }));
        var recovery = enabled["recoveryCodes"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal(8, recovery.Count);

        // password alone is no longer enough
        var login = await Body(await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email, password = Pw }));
        Assert.True((bool)login["requiresTwoFactor"]!);
        Assert.Null((string?)login["accessToken"]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email, password = Pw, twoFactorCode = "111111" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email, password = Pw, twoFactorCode = Totp(secret) })).StatusCode);

        // a recovery code works once
        Assert.Equal(HttpStatusCode.OK, (await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email, password = Pw, twoFactorCode = recovery[0] })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email, password = Pw, twoFactorCode = recovery[0] })).StatusCode);

        // switching off needs the password and a valid code
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/auth/2fa/disable", new { password = "wrong", code = Totp(secret) })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync("/api/v1/auth/2fa/disable", new { password = Pw, code = Totp(secret) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email, password = Pw })).StatusCode);
    }

    [Fact]
    public async Task Two_factor_endpoints_need_a_signed_in_user()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsync("/api/v1/auth/2fa/setup", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync("/api/v1/auth/2fa")).StatusCode);
    }
}
