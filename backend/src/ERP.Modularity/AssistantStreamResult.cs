using System.Text.Json;
using ERP.Application.Assistant;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ERP.Modularity;

/// <summary>
/// Sends a reply as it is written, as server-sent events. Anything that fails before the first event (no permission, invalid input) still
/// answers with the normal error envelope, because nothing has been sent yet. Keep-alive comments stop proxies closing a quiet connection.
/// </summary>
public sealed class AssistantStreamResult(IAsyncEnumerable<AssistantEvent> events) : IResult
{
    private static readonly TimeSpan KeepAlive = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    public async Task ExecuteAsync(HttpContext http)
    {
        var ct = http.RequestAborted;
        await using var stream = events.GetAsyncEnumerator(ct);
        if (!await stream.MoveNextAsync()) return;

        http.Response.StatusCode = StatusCodes.Status200OK;
        http.Response.ContentType = "text/event-stream; charset=utf-8";
        http.Response.Headers.CacheControl = "no-cache, no-transform";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        Task<bool>? next = null;
        try
        {
            await WriteAsync(http, stream.Current, ct);
            next = stream.MoveNextAsync().AsTask();
            while (true)
            {
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
                if (await Task.WhenAny(next, Task.Delay(KeepAlive, wait.Token)) != next)
                {
                    await http.Response.WriteAsync(": keep-alive\n\n", ct);
                    await http.Response.Body.FlushAsync(ct);
                    continue;
                }
                wait.Cancel();
                if (!await next) break;
                await WriteAsync(http, stream.Current, ct);
                next = stream.MoveNextAsync().AsTask();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The person closed the page or pressed Stop.
        }
        catch (Exception e)
        {
            http.RequestServices.GetRequiredService<ILogger<AssistantStreamResult>>().LogError(e, "The assistant reply failed part way through");
            if (!ct.IsCancellationRequested) await WriteAsync(http, new AssistantError("failed", "Something went wrong. Please try again."), CancellationToken.None);
        }
        finally
        {
            // A reply cannot be closed while its next step is still running: that throws, and what it used would never be saved.
            if (next is not null) await Task.WhenAny(next);
        }
    }

    private static async Task WriteAsync(HttpContext http, AssistantEvent e, CancellationToken ct)
    {
        var (name, data) = e switch
        {
            AssistantDelta d => ("delta", JsonSerializer.Serialize(new { d.Text }, Json)),
            AssistantStatus s => ("status", JsonSerializer.Serialize(new { s.Text }, Json)),
            AssistantServiceUsed u => ("service", JsonSerializer.Serialize(new { u.Name }, Json)),
            AssistantDone d => ("done", JsonSerializer.Serialize(new { d.InputTokens, d.OutputTokens }, Json)),
            AssistantError x => ("error", JsonSerializer.Serialize(new { x.Code, x.Message }, Json)),
            AssistantData x => (x.Name, JsonSerializer.Serialize(x.Payload, Json)),
            _ => throw new InvalidOperationException("Unknown assistant event."),
        };
        await http.Response.WriteAsync($"event: {name}\ndata: {data}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }
}

/// <summary>Marks an endpoint that changes nothing in the business, so a view-only workspace can still use it even though it is a POST.</summary>
public sealed record AllowedInReadOnlyWorkspace;
