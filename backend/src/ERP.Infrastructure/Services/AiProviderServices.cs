using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using ERP.Application.Platform;
using Microsoft.AspNetCore.DataProtection;

namespace ERP.Infrastructure.Services;

public sealed class DataProtectionAiKeyProtector(IDataProtectionProvider dataProtection) : IAiKeyProtector
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("erp.ai-provider-key.v1");

    public string Protect(string apiKey) => _protector.Protect(apiKey);

    public string? TryUnprotect(string encrypted)
    {
        try { return _protector.Unprotect(encrypted); }
        catch (CryptographicException) { return null; }
    }
}

/// <summary>Tests a service that uses the common OpenAI-style chat format. Messages come from a fixed list, so neither the key nor the service's reply can reach the database or a log.</summary>
public sealed class OpenAiStyleConnectionTester(IHttpClientFactory clients) : IAiConnectionTester
{
    public const string ClientName = "ai-provider";

    public async Task<AiConnectionResult> TestAsync(string baseUrl, string model, string apiKey, CancellationToken ct = default)
    {
        if (AiEndpointRules.Problem(baseUrl) is { } problem) return new(false, problem);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{AiEndpointRules.Normalize(baseUrl)}/chat/completions")
        {
            Content = JsonContent.Create(new { model, messages = new[] { new { role = "user", content = "Reply with the word OK." } }, max_tokens = 16, stream = false }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        try
        {
            using var response = await clients.CreateClient(ClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            return Describe(response.StatusCode);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(false, "The service did not answer in time."); }
        catch (HttpRequestException) { return new(false, "Could not reach the service. Check the address."); }
    }

    public static AiConnectionResult Describe(HttpStatusCode status) => (int)status switch
    {
        >= 200 and < 300 => new(true, "Connected. The model answered."),
        >= 300 and < 400 => new(false, "The address redirects somewhere else. Use the final address."),
        400 => new(false, "The service refused the request. Check the model name."),
        401 or 403 => new(false, "The service rejected the key."),
        404 => new(false, "The address or model was not found."),
        429 => new(false, "The key was accepted, but its usage limit has been reached for now."),
        >= 500 => new(false, "The service had a problem. Try again later."),
        _ => new(false, $"The service answered with status {(int)status}."),
    };

    /// <summary>The handler behind the client: no redirects, and it refuses to connect to a private address even if a public name resolves to one.</summary>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            if (addresses.Length == 0 || addresses.Any(AiEndpointRules.IsPrivate)) throw new HttpRequestException("That address is not allowed.");
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };
}

/// <summary>A token a visitor's browser holds for its conversation. It expires, so an old link to a chat stops working.</summary>
public sealed class BrandSessionTokens(IDataProtectionProvider dataProtection) : IBrandSessionTokens
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);
    private readonly ITimeLimitedDataProtector _protector = dataProtection.CreateProtector("erp.brand-assistant-session.v1").ToTimeLimitedDataProtector();

    public string Issue(Guid conversationId) => _protector.Protect(conversationId.ToString("N"), Lifetime);

    public Guid? Read(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try { return Guid.TryParse(_protector.Unprotect(token), out var id) ? id : null; }
        catch (CryptographicException) { return null; }
    }
}
