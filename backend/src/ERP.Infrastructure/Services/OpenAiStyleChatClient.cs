using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ERP.Application.Assistant;
using ERP.Application.Platform;

namespace ERP.Infrastructure.Services;

/// <summary>Talks to any service that uses the common OpenAI-style streaming chat format.</summary>
public sealed class OpenAiStyleChatClient(IHttpClientFactory clients) : IChatModelClient
{
    /// <summary>A stream that goes quiet for this long has stalled.</summary>
    private static readonly TimeSpan IdleLimit = TimeSpan.FromSeconds(30);

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(AiServiceConnection connection, ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (AiEndpointRules.Problem(connection.BaseUrl) is { } problem) throw new ChatServiceException(ChatFailureKind.NotAllowed, problem);

        using var response = await SendAsync(connection, request, ct);
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(body, Encoding.UTF8);
        var filter = new ThinkTagFilter();
        var calls = new SortedDictionary<int, PendingToolCall>();
        ChatUsage? usage = null;

        while (true)
        {
            var line = await ReadLineAsync(reader, ct);
            if (line is null) break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line[5..].Trim();
            if (data == "[DONE]") break;
            if (data.Length == 0) continue;

            var chunk = Parse(data);
            if (chunk["error"] is not null) throw new ChatServiceException(ChatFailureKind.Unavailable, "The service had a problem. Try again later.");
            usage = ReadUsage(chunk["usage"]) ?? ReadUsage(chunk["x_groq"]?["usage"]) ?? usage;

            if (chunk["choices"] is not JsonArray { Count: > 0 } choices || choices[0]?["delta"] is not JsonObject delta) continue;
            if (delta["content"] is JsonValue content && content.TryGetValue<string>(out var text) && text.Length > 0)
            {
                var visible = filter.Feed(text);
                if (visible.Length > 0) yield return new ChatTextDelta(visible);
            }
            if (delta["tool_calls"] is JsonArray toolCalls)
                foreach (var part in toolCalls) Merge(calls, part);
        }

        var rest = filter.Flush();
        if (rest.Length > 0) yield return new ChatTextDelta(rest);
        foreach (var call in calls.Values)
            if (!string.IsNullOrEmpty(call.Name)) yield return new ChatToolCallReady(new ChatToolCall(call.Id ?? call.Name, call.Name, call.Arguments.Length > 0 ? call.Arguments.ToString() : "{}"));
        if (usage is not null) yield return usage;
    }

    private async Task<HttpResponseMessage> SendAsync(AiServiceConnection connection, ChatRequest request, CancellationToken ct)
    {
        var response = await PostAsync(connection, request, includeUsage: true, ct);
        // Not every service accepts the option that asks for usage, so a refusal gets one try without it.
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            response.Dispose();
            response = await PostAsync(connection, request, includeUsage: false, ct);
        }
        if (response.IsSuccessStatusCode) return response;

        var status = response.StatusCode;
        var retryAfter = response.Headers.RetryAfter?.Delta;
        response.Dispose();
        throw new ChatServiceException(KindOf(status), OpenAiStyleConnectionTester.Describe(status).Message, retryAfter);
    }

    private async Task<HttpResponseMessage> PostAsync(AiServiceConnection connection, ChatRequest request, bool includeUsage, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{AiEndpointRules.Normalize(connection.BaseUrl)}/chat/completions")
        {
            Content = new StringContent(BuildBody(connection.Model, request, includeUsage).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);
        try
        {
            return await clients.CreateClient(OpenAiStyleConnectionTester.ClientName).SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ChatServiceException(ChatFailureKind.TimedOut, "The service did not answer in time."); }
        catch (HttpRequestException) { throw new ChatServiceException(ChatFailureKind.Unavailable, "Could not reach the service. Check the address."); }
    }

    private static async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken ct)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
        idle.CancelAfter(IdleLimit);
        try { return await reader.ReadLineAsync(idle.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ChatServiceException(ChatFailureKind.TimedOut, "The service stopped answering."); }
        catch (IOException) { throw new ChatServiceException(ChatFailureKind.Unavailable, "The connection to the service was lost."); }
    }

    private static JsonNode Parse(string data)
    {
        try { return JsonNode.Parse(data) ?? new JsonObject(); }
        catch (JsonException) { throw new ChatServiceException(ChatFailureKind.Unavailable, "The service sent a reply that could not be read."); }
    }

    private static ChatUsage? ReadUsage(JsonNode? node)
        => node is JsonObject && node["prompt_tokens"] is JsonValue input && node["completion_tokens"] is JsonValue output ? new ChatUsage(input.GetValue<int>(), output.GetValue<int>()) : null;

    private static void Merge(SortedDictionary<int, PendingToolCall> calls, JsonNode? part)
    {
        if (part is null) return;
        var index = part["index"] is JsonValue i && i.TryGetValue<int>(out var n) ? n : calls.Count;
        if (!calls.TryGetValue(index, out var call)) calls[index] = call = new PendingToolCall();
        call.Id ??= (string?)part["id"];
        if (part["function"] is not { } function) return;
        call.Name ??= (string?)function["name"];
        // Some services send the arguments as a finished object rather than as text.
        if (function["arguments"] is JsonValue text && text.TryGetValue<string>(out var piece)) call.Arguments.Append(piece);
        else if (function["arguments"] is JsonObject whole) call.Arguments.Append(whole.ToJsonString());
    }

    private static JsonObject BuildBody(string model, ChatRequest request, bool includeUsage)
    {
        var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = request.System } };
        foreach (var m in request.Messages) messages.Add(ToJson(m));
        var body = new JsonObject { ["model"] = model, ["messages"] = messages, ["max_tokens"] = request.MaxOutputTokens, ["stream"] = true };
        if (includeUsage) body["stream_options"] = new JsonObject { ["include_usage"] = true };
        if (request.Tools.Count > 0)
        {
            var tools = new JsonArray();
            foreach (var t in request.Tools)
                tools.Add(new JsonObject { ["type"] = "function", ["function"] = new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["parameters"] = JsonNode.Parse(t.ParametersJsonSchema) } });
            body["tools"] = tools;
        }
        return body;
    }

    private static JsonObject ToJson(ChatMessage m) => m.Role switch
    {
        ChatRole.Tool => new JsonObject { ["role"] = "tool", ["tool_call_id"] = m.ToolCallId, ["content"] = m.Text ?? "" },
        ChatRole.Assistant when m.ToolCalls is { Count: > 0 } calls => new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = m.Text,
            ["tool_calls"] = new JsonArray(calls.Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c.Id, ["type"] = "function", ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.ArgumentsJson },
            }).ToArray()),
        },
        ChatRole.Assistant => new JsonObject { ["role"] = "assistant", ["content"] = m.Text ?? "" },
        _ => new JsonObject { ["role"] = "user", ["content"] = m.Text ?? "" },
    };

    private static ChatFailureKind KindOf(HttpStatusCode status) => (int)status switch
    {
        400 => ChatFailureKind.BadRequest,
        401 or 403 => ChatFailureKind.KeyRejected,
        404 => ChatFailureKind.NotFound,
        429 => ChatFailureKind.RateLimited,
        _ => ChatFailureKind.Unavailable,
    };

    private sealed class PendingToolCall
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public StringBuilder Arguments { get; } = new();
    }
}

/// <summary>Some models write their private reasoning between think tags in the reply. It must never reach the person, even when a tag is split across two pieces.</summary>
internal sealed class ThinkTagFilter
{
    private const string Open = "<think>";
    private const string Close = "</think>";
    private string _pending = "";
    private bool _inside;
    private bool _startOfReply = true;

    public string Feed(string piece)
    {
        var text = _pending + piece;
        _pending = "";
        var visible = new StringBuilder();
        while (text.Length > 0)
        {
            var tag = _inside ? Close : Open;
            var at = text.IndexOf(tag, StringComparison.Ordinal);
            if (at >= 0)
            {
                if (!_inside) visible.Append(text[..at]);
                text = text[(at + tag.Length)..];
                _inside = !_inside;
                continue;
            }
            var keep = PartialTagLength(text, tag);
            if (!_inside) visible.Append(text[..^keep]);
            _pending = text[^keep..];
            break;
        }
        return Trimmed(visible.ToString());
    }

    public string Flush()
    {
        var rest = _inside ? "" : _pending;
        _pending = "";
        return Trimmed(rest);
    }

    /// <summary>A reply that followed a think block starts with blank lines.</summary>
    private string Trimmed(string text)
    {
        if (!_startOfReply) return text;
        text = text.TrimStart();
        if (text.Length > 0) _startOfReply = false;
        return text;
    }

    private static int PartialTagLength(string text, string tag)
    {
        for (var length = Math.Min(tag.Length - 1, text.Length); length > 0; length--)
            if (text.EndsWith(tag[..length], StringComparison.Ordinal)) return length;
        return 0;
    }
}
