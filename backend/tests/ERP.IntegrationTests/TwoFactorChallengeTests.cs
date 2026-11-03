using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ERP.IntegrationTests;

/// <summary>A clock the tests can freeze and move; real time when not frozen.</summary>
public sealed class TestClock : IClock
{
    public DateTime? Frozen { get; set; }
    public DateTime UtcNow => Frozen ?? DateTime.UtcNow;

    /// <summary>Freezes the clock 10 seconds into the current 30-second step, so a test never straddles a step boundary by accident.</summary>
    public DateTime FreezeNow()
    {
        var step = Totp.StepAt(DateTime.UtcNow);
        Frozen = DateTime.UnixEpoch.AddSeconds(step * Totp.PeriodSeconds + 10);
        return Frozen.Value;
    }

    public void Advance(TimeSpan by) => Frozen = (Frozen ?? DateTime.UtcNow) + by;
}

/// <summary>API host with Auth:TwoFactor:Enforce on and a controllable clock.</summary>
public sealed class TwoFactorEnforcedFactory : ApiFactory
{
    public TestClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:TwoFactor:Enforce", "true");
        builder.UseSetting("Auth:TwoFactor:Issuer", "Acme Cloud");
        builder.ConfigureServices(s => { s.RemoveAll<IClock>(); s.AddSingleton<IClock>(Clock); });
    }

    public TwoFactorOptions TwoFactor => Services.GetRequiredService<IOptions<AuthOptions>>().Value.TwoFactor;
}

/// <summary>First-sign-in authenticator setup and returning-user code entry: login → challenge → setup → verify → session.</summary>
public sealed class TwoFactorChallengeTests(TwoFactorEnforcedFactory f) : IClassFixture<TwoFactorEnforcedFactory>, IDisposable
{
    private const string Pw = "Temp#Passw0rd";
    private const string Login = "/api/v1/auth/login";
    private const string Setup = "/api/v1/auth/login/2fa/setup";
    private const string Verify = "/api/v1/auth/login/2fa/verify";

    public void Dispose() { f.Clock.Frozen = null; f.TwoFactor.Enforce = true; }

    /// <summary>A new single-workspace user (created by the admin while enforcement is briefly off, since the admin has no authenticator).</summary>
    private async Task<string> NewUserAsync()
    {
        f.TwoFactor.Enforce = false;
        try
        {
            var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
            var roles = ApiFactory.Unwrap(JsonNode.Parse(await (await admin.GetAsync("/api/v1/roles")).Content.ReadAsStringAsync())!);
            var role = (string)roles.AsArray().First(r => (string?)r!["name"] == "Viewer")!["id"]!;
            var email = $"tc{Guid.NewGuid():N}"[..10] + "@demo.local";
            (await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Challenge User", temporaryPassword = Pw, roleIds = new[] { role } })).EnsureSuccessStatusCode();
            return email;
        }
        finally { f.TwoFactor.Enforce = true; }
    }

    private static async Task<JsonNode> Body(HttpResponseMessage r, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == expected, $"Expected {expected} but got {r.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private string CodeAt(string secret, int stepOffset = 0) => Totp.Code(Totp.DecodeBase32(secret), Totp.StepAt(f.Clock.UtcNow) + stepOffset);

    /// <summary>Signs in a new user and completes the first-sign-in setup; returns the secret.</summary>
    private async Task<(string Email, string Secret)> EnrolledUserAsync()
    {
        var email = await NewUserAsync();
        var login = await Body(await f.Client().PostAsJsonAsync(Login, new { email, password = Pw }));
        var token = (string)login["challengeToken"]!;
        var secret = (string)(await Body(await f.Client().PostAsJsonAsync(Setup, new { challengeToken = token })))["secret"]!;
        await Body(await f.Client().PostAsJsonAsync(Verify, new { challengeToken = token, code = CodeAt(secret) }));
        return (email, secret);
    }

    [Fact]
    public async Task First_sign_in_sets_up_the_authenticator_before_any_session_is_issued()
    {
        f.Clock.FreezeNow();
        var email = await NewUserAsync();
        var client = f.Client();

        var res = await client.PostAsJsonAsync(Login, new { email, password = Pw });
        var login = await Body(res);
        Assert.True((bool)login["twoFactorSetupRequired"]!);
        Assert.Null(login["accessToken"]);
        Assert.Equal(600, (int)login["challengeExpiresIn"]!);
        Assert.False(res.Headers.TryGetValues("Set-Cookie", out _), "no refresh cookie before the second factor");
        var token = (string)login["challengeToken"]!;

        // the challenge is not a session
        var bearer = f.Client();
        bearer.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearer.GetAsync("/api/v1/me")).StatusCode);

        var setup = await Body(await client.PostAsJsonAsync(Setup, new { challengeToken = token }));
        var secret = ((string)setup["secret"]!).Replace(" ", "");
        Assert.Matches("^[A-Z2-7]{16,}$", secret);
        Assert.Matches("^([A-Z2-7]{4} )*[A-Z2-7]{1,4}$", (string)setup["secret"]!);
        Assert.Equal($"otpauth://totp/Acme%20Cloud:{Uri.EscapeDataString(email)}?secret={secret}&issuer=Acme%20Cloud&digits=6&period=30", (string)setup["otpAuthUri"]!);
        Assert.Equal("Acme Cloud", (string)setup["issuer"]!);
        Assert.Equal(email, (string)setup["account"]!);
        Assert.Equal(6, (int)setup["digits"]!);
        Assert.Equal(30, (int)setup["period"]!);
        var qr = (string)setup["qrCodeDataUri"]!;
        Assert.StartsWith("data:image/png;base64,", qr);
        var png = Convert.FromBase64String(qr["data:image/png;base64,".Length..]);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Verify, new { challengeToken = token, code = "000000" == CodeAt(secret) ? "111111" : "000000" })).StatusCode);

        res = await client.PostAsJsonAsync(Verify, new { challengeToken = token, code = CodeAt(secret) });
        var session = await Body(res);
        Assert.False(string.IsNullOrEmpty((string?)session["accessToken"]));
        Assert.Equal(8, session["recoveryCodes"]!.AsArray().Count);
        Assert.Contains(res.Headers.GetValues("Set-Cookie"), c => c.StartsWith("erp_rt="));

        var signedIn = f.Client();
        signedIn.DefaultRequestHeaders.Authorization = new("Bearer", (string)session["accessToken"]!);
        Assert.True((bool)(await Body(await signedIn.GetAsync("/api/v1/auth/2fa")))["enabled"]!);

        // the setup challenge is spent: 2FA is on now, so it cannot start another setup or confirm again
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Setup, new { challengeToken = token })).StatusCode);
    }

    [Fact]
    public async Task Returning_user_gets_a_verify_challenge_and_codes_cannot_be_replayed()
    {
        f.Clock.FreezeNow();
        var (email, secret) = await EnrolledUserAsync();

        var login = await Body(await f.Client().PostAsJsonAsync(Login, new { email, password = Pw }));
        Assert.True((bool)login["twoFactorRequired"]!);
        Assert.True((bool)login["requiresTwoFactor"]!); // kept for older clients
        Assert.Null(login["twoFactorSetupRequired"]);
        Assert.Null(login["accessToken"]);
        var token = (string)login["challengeToken"]!;

        // a verify challenge cannot be used to replace the authenticator
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync(Setup, new { challengeToken = token })).StatusCode);

        // the code that confirmed the setup was used in this step: replay refused
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync(Verify, new { challengeToken = token, code = CodeAt(secret) })).StatusCode);

        f.Clock.Advance(TimeSpan.FromSeconds(30));
        var session = await Body(await f.Client().PostAsJsonAsync(Verify, new { challengeToken = token, code = CodeAt(secret) }));
        Assert.False(string.IsNullOrEmpty((string?)session["accessToken"]));
        Assert.Null(session["recoveryCodes"]);

        // and the same code again, even with a fresh challenge, is a replay
        var again = (string)(await Body(await f.Client().PostAsJsonAsync(Login, new { email, password = Pw })))["challengeToken"]!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync(Verify, new { challengeToken = again, code = CodeAt(secret) })).StatusCode);
    }

    [Fact]
    public async Task One_step_of_clock_drift_is_accepted_but_two_are_not()
    {
        f.Clock.FreezeNow();
        var (email, secret) = await EnrolledUserAsync();
        f.Clock.Advance(TimeSpan.FromMinutes(2)); // well past the step used to enrol

        async Task<HttpStatusCode> VerifyWith(string code)
        {
            var challenge = (string)(await Body(await f.Client().PostAsJsonAsync(Login, new { email, password = Pw })))["challengeToken"]!;
            return (await f.Client().PostAsJsonAsync(Verify, new { challengeToken = challenge, code })).StatusCode;
        }

        Assert.Equal(HttpStatusCode.Unauthorized, await VerifyWith(CodeAt(secret, -2)));
        Assert.Equal(HttpStatusCode.Unauthorized, await VerifyWith(CodeAt(secret, 2)));
        Assert.Equal(HttpStatusCode.OK, await VerifyWith(CodeAt(secret, -1)));
        Assert.Equal(HttpStatusCode.OK, await VerifyWith(CodeAt(secret, 1)));
    }

    [Fact]
    public async Task A_recovery_code_completes_a_verify_challenge_once()
    {
        f.Clock.FreezeNow();
        var email = await NewUserAsync();
        var token = (string)(await Body(await f.Client().PostAsJsonAsync(Login, new { email, password = Pw })))["challengeToken"]!;
        var secret = (string)(await Body(await f.Client().PostAsJsonAsync(Setup, new { challengeToken = token })))["secret"]!;
        var recovery = (string)(await Body(await f.Client().PostAsJsonAsync(Verify, new { challengeToken = token, code = CodeAt(secret) })))["recoveryCodes"]![0]!;

        var c1 = (string)(await Body(await f.Client().PostAsJsonAsync(Login, new { email, password = Pw })))["challengeToken"]!;
        Assert.Equal(HttpStatusCode.OK, (await f.Client().PostAsJsonAsync(Verify, new { challengeToken = c1, code = recovery })).StatusCode);
        var c2 = (string)(await Body(await f.Client().PostAsJsonAsync(Login, new { email, password = Pw })))["challengeToken"]!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync(Verify, new { challengeToken = c2, code = recovery })).StatusCode);
    }

    [Fact]
    public async Task An_expired_or_tampered_challenge_is_refused()
    {
        f.Clock.FreezeNow();
        var email = await NewUserAsync();
        var token = (string)(await Body(await f.Client().PostAsJsonAsync(Login, new { email, password = Pw })))["challengeToken"]!;

        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync(Setup, new { challengeToken = token[..^4] + "AAAA" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync(Setup, new { challengeToken = "not-a-token" })).StatusCode);

        var secret = (string)(await Body(await f.Client().PostAsJsonAsync(Setup, new { challengeToken = token })))["secret"]!;
        f.Clock.Advance(TimeSpan.FromMinutes(10).Add(TimeSpan.FromSeconds(1)));
        var expired = await f.Client().PostAsJsonAsync(Verify, new { challengeToken = token, code = CodeAt(secret) });
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Contains("expired", await expired.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync(Setup, new { challengeToken = token })).StatusCode);
    }

    [Fact]
    public async Task Repeated_wrong_codes_lock_the_account()
    {
        f.Clock.FreezeNow();
        var email = await NewUserAsync();
        var token = (string)(await Body(await f.Client().PostAsJsonAsync(Login, new { email, password = Pw })))["challengeToken"]!;
        var secret = (string)(await Body(await f.Client().PostAsJsonAsync(Setup, new { challengeToken = token })))["secret"]!;
        var right = CodeAt(secret);
        var wrong = right == "123456" ? "654321" : "123456";

        for (var i = 1; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync(Verify, new { challengeToken = token, code = wrong })).StatusCode);
        Assert.Equal((HttpStatusCode)423, (await f.Client().PostAsJsonAsync(Verify, new { challengeToken = token, code = wrong })).StatusCode);
        // locked: even the right code (and the password) is refused until the lockout ends
        Assert.Equal((HttpStatusCode)423, (await f.Client().PostAsJsonAsync(Verify, new { challengeToken = token, code = right })).StatusCode);
        Assert.Equal((HttpStatusCode)423, (await f.Client().PostAsJsonAsync(Login, new { email, password = Pw })).StatusCode);
    }

    [Fact]
    public async Task With_enforcement_off_a_password_still_signs_in_users_without_an_authenticator()
    {
        var email = await NewUserAsync();
        f.TwoFactor.Enforce = false;
        var session = await Body(await f.Client().PostAsJsonAsync(Login, new { email, password = Pw }));
        Assert.False(string.IsNullOrEmpty((string?)session["accessToken"]));
        Assert.Null(session["twoFactorSetupRequired"]);
        Assert.Null(session["challengeToken"]);
    }

    [Fact]
    public async Task Bad_input_is_a_validation_error()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client().PostAsJsonAsync(Setup, new { challengeToken = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client().PostAsJsonAsync(Verify, new { challengeToken = "x", code = "" })).StatusCode);
    }
}
