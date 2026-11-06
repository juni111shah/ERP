using System.Net;
using ERP.Application.Platform;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.UnitTests;

public class AiEndpointRulesTests
{
    [Theory]
    [InlineData("https://api.example.com/v1")]
    [InlineData("https://api.example.com/v1/")]
    [InlineData("  https://api.example.com/openai/v1  ")]
    [InlineData("https://8.8.8.8/v1")]
    public void Public_secure_addresses_are_allowed(string url) => Assert.Null(AiEndpointRules.Problem(url));

    [Theory]
    [InlineData("")]
    [InlineData("api.example.com")]
    [InlineData("http://api.example.com")]
    [InlineData("ftp://api.example.com")]
    [InlineData("https://localhost")]
    [InlineData("https://db.local")]
    [InlineData("https://svc.internal")]
    [InlineData("https://nas")]
    [InlineData("https://10.1.2.3")]
    [InlineData("https://172.16.0.1")]
    [InlineData("https://192.168.0.1")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://169.254.169.254")]
    [InlineData("https://[::1]")]
    [InlineData("https://[fd00::1]")]
    [InlineData("https://[::ffff:10.0.0.1]")]
    [InlineData("https://user:pass@api.example.com")]
    [InlineData("https://api.example.com/v1?x=1")]
    public void Other_addresses_are_refused(string url) => Assert.NotNull(AiEndpointRules.Problem(url));

    [Fact]
    public void A_trailing_slash_is_removed() => Assert.Equal("https://api.example.com/v1", AiEndpointRules.Normalize(" https://api.example.com/v1/ "));
}

public class AiKeyProtectorTests
{
    private static DataProtectionAiKeyProtector Create(string app)
    {
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName(app).UseEphemeralDataProtectionProvider();
        return new DataProtectionAiKeyProtector(services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>());
    }

    [Fact]
    public void A_key_is_stored_scrambled_and_reads_back()
    {
        var protector = Create("test");
        var stored = protector.Protect("secret-key");
        Assert.DoesNotContain("secret-key", stored);
        Assert.Equal("secret-key", protector.TryUnprotect(stored));
    }

    [Fact]
    public void A_value_it_cannot_read_gives_null_rather_than_an_error()
    {
        var stored = Create("one").Protect("secret-key");
        Assert.Null(Create("two").TryUnprotect(stored));
        Assert.Null(Create("two").TryUnprotect("not-encrypted"));
    }
}

public class OpenAiStyleConnectionTesterTests
{
    private const string Key = "secret-key-value";

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return respond(request);
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static Task<AiConnectionResult> Run(StubHandler handler, string baseUrl = "https://api.example.com/v1/")
        => new OpenAiStyleConnectionTester(new Factory(handler)).TestAsync(baseUrl, "model-a", Key);

    [Fact]
    public async Task It_makes_one_small_chat_call_with_the_key_in_the_header()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var result = await Run(handler);
        Assert.True(result.Ok);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.example.com/v1/chat/completions", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal(Key, handler.Request.Headers.Authorization.Parameter);
        Assert.Contains("\"model\":\"model-a\"", handler.Body);
        Assert.DoesNotContain(Key, handler.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "model")]
    [InlineData(HttpStatusCode.Unauthorized, "key")]
    [InlineData(HttpStatusCode.Forbidden, "key")]
    [InlineData(HttpStatusCode.NotFound, "not found")]
    [InlineData(HttpStatusCode.TooManyRequests, "limit")]
    [InlineData(HttpStatusCode.InternalServerError, "problem")]
    [InlineData(HttpStatusCode.Redirect, "redirects")]
    public async Task A_failure_says_what_to_check_and_never_repeats_the_reply(HttpStatusCode status, string expected)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status) { Content = new StringContent($"bad key {Key}") });
        var result = await Run(handler);
        Assert.False(result.Ok);
        Assert.Contains(expected, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Key, result.Message);
    }

    [Fact]
    public async Task A_network_failure_is_a_result_not_a_crash()
    {
        var result = await Run(new StubHandler(_ => throw new HttpRequestException($"failed {Key}")));
        Assert.False(result.Ok);
        Assert.DoesNotContain(Key, result.Message);
    }

    [Fact]
    public async Task A_timeout_is_a_result_not_a_crash()
    {
        var result = await Run(new StubHandler(_ => throw new TaskCanceledException()));
        Assert.False(result.Ok);
        Assert.Contains("time", result.Message);
    }

    [Fact]
    public async Task It_refuses_a_private_address_before_sending_anything()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var result = await Run(handler, "https://192.168.1.5/v1");
        Assert.False(result.Ok);
        Assert.Null(handler.Request);
    }
}
