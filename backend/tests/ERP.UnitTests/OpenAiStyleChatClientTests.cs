using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ERP.Application.Assistant;
using ERP.Infrastructure.Services;

namespace ERP.UnitTests;

public class OpenAiStyleChatClientTests
{
    private const string Key = "secret-key-value";
    private static readonly AiServiceConnection Connection = new("https://api.example.com/v1/", "model-a", Key);
    private static ChatRequest Request(params ChatMessage[] messages) => new("Be brief.", messages.Length > 0 ? messages : [new ChatMessage(ChatRole.User, "Hello")], [], 100);

    private sealed class StubHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] replies) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add((request, await request.Content!.ReadAsStringAsync(ct)));
            return replies[Math.Min(Calls.Count - 1, replies.Length - 1)](request);
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> Sse(params string[] chunks) => _ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(string.Concat(chunks.Select(c => $"data: {c}\n\n")) + "data: [DONE]\n\n", Encoding.UTF8, "text/event-stream"),
    };

    private static string Text(string text) => new JsonObject { ["choices"] = new JsonArray(new JsonObject { ["delta"] = new JsonObject { ["content"] = text } }) }.ToJsonString();

    private static async Task<List<ChatStreamEvent>> Run(StubHandler handler, ChatRequest? request = null)
    {
        var events = new List<ChatStreamEvent>();
        await foreach (var e in new OpenAiStyleChatClient(new Factory(handler)).StreamAsync(Connection, request ?? Request())) events.Add(e);
        return events;
    }

    private static string Joined(IEnumerable<ChatStreamEvent> events) => string.Concat(events.OfType<ChatTextDelta>().Select(d => d.Text));

    [Fact]
    public async Task It_streams_the_reply_and_reports_usage()
    {
        var handler = new StubHandler(Sse(Text("Hel"), Text("lo"), """{"choices":[],"usage":{"prompt_tokens":12,"completion_tokens":3}}"""));
        var events = await Run(handler);

        Assert.Equal("Hello", Joined(events));
        Assert.Equal(new ChatUsage(12, 3), events.OfType<ChatUsage>().Single());
        var (request, body) = handler.Calls.Single();
        Assert.Equal("https://api.example.com/v1/chat/completions", request.RequestUri!.ToString());
        Assert.Equal(Key, request.Headers.Authorization!.Parameter);
        Assert.DoesNotContain(Key, body);
        var json = JsonNode.Parse(body)!;
        Assert.Equal("model-a", (string)json["model"]!);
        Assert.True((bool)json["stream"]!);
        Assert.Equal("system", (string)json["messages"]![0]!["role"]!);
        Assert.True((bool)json["stream_options"]!["include_usage"]!);
    }

    [Fact]
    public async Task It_accepts_the_usage_field_one_service_uses_instead()
    {
        var events = await Run(new StubHandler(Sse(Text("Hi"), """{"choices":[],"x_groq":{"usage":{"prompt_tokens":5,"completion_tokens":1}}}""")));
        Assert.Equal(new ChatUsage(5, 1), events.OfType<ChatUsage>().Single());
    }

    [Fact]
    public async Task A_service_that_refuses_the_usage_option_gets_one_try_without_it()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest), Sse(Text("Hi")));
        var events = await Run(handler);

        Assert.Equal("Hi", Joined(events));
        Assert.Equal(2, handler.Calls.Count);
        Assert.Null(JsonNode.Parse(handler.Calls[1].Body)!["stream_options"]);
    }

    [Fact]
    public async Task Tool_calls_arrive_in_pieces_and_come_out_whole()
    {
        var events = await Run(new StubHandler(Sse(
            """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"get_plans","arguments":""}}]}}]}""",
            """{"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{\"users\":"}}]}}]}""",
            """{"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"12}"}}]}}]}""",
            """{"choices":[{"delta":{"tool_calls":[{"index":1,"id":"call_2","function":{"name":"get_modules","arguments":{"all":true}}}]}}]}""")));

        var calls = events.OfType<ChatToolCallReady>().Select(c => c.Call).ToList();
        Assert.Equal(new ChatToolCall("call_1", "get_plans", """{"users":12}"""), calls[0]);
        Assert.Equal("get_modules", calls[1].Name);
        Assert.Equal("""{"all":true}""", calls[1].ArgumentsJson);
    }

    [Fact]
    public async Task Tool_results_and_tools_are_sent_in_the_service_format()
    {
        var call = new ChatToolCall("call_1", "get_plans", "{}");
        var handler = new StubHandler(Sse(Text("Done")));
        await Run(handler, new ChatRequest("Be brief.", [
            new ChatMessage(ChatRole.User, "Plans?"), new ChatMessage(ChatRole.Assistant, null, [call]), new ChatMessage(ChatRole.Tool, "Starter", ToolCallId: "call_1"),
        ], [new ChatToolDefinition("get_plans", "Lists plans", """{"type":"object","properties":{}}""")], 100));

        var json = JsonNode.Parse(handler.Calls[0].Body)!;
        Assert.Equal("get_plans", (string)json["messages"]![2]!["tool_calls"]![0]!["function"]!["name"]!);
        Assert.Equal("tool", (string)json["messages"]![3]!["role"]!);
        Assert.Equal("call_1", (string)json["messages"]![3]!["tool_call_id"]!);
        Assert.Equal("object", (string)json["tools"]![0]!["function"]!["parameters"]!["type"]!);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ChatFailureKind.KeyRejected)]
    [InlineData(HttpStatusCode.Forbidden, ChatFailureKind.KeyRejected)]
    [InlineData(HttpStatusCode.NotFound, ChatFailureKind.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, ChatFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.BadGateway, ChatFailureKind.Unavailable)]
    public async Task A_failure_names_its_kind_and_never_repeats_the_reply(HttpStatusCode status, ChatFailureKind kind)
    {
        var e = await Assert.ThrowsAsync<ChatServiceException>(() => Run(new StubHandler(_ => new HttpResponseMessage(status) { Content = new StringContent($"bad {Key}") })));
        Assert.Equal(kind, e.Kind);
        Assert.DoesNotContain(Key, e.Message);
    }

    [Fact]
    public async Task A_limit_reply_says_how_long_to_wait()
    {
        var e = await Assert.ThrowsAsync<ChatServiceException>(() => Run(new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
            return response;
        })));
        Assert.Equal(TimeSpan.FromSeconds(42), e.RetryAfter);
    }

    [Fact]
    public async Task An_error_inside_the_stream_is_a_failure()
    {
        var e = await Assert.ThrowsAsync<ChatServiceException>(() => Run(new StubHandler(Sse(Text("Par"), """{"error":{"message":"overloaded"}}"""))));
        Assert.Equal(ChatFailureKind.Unavailable, e.Kind);
    }

    [Fact]
    public async Task It_refuses_a_private_address_before_sending_anything()
    {
        var handler = new StubHandler(Sse(Text("Hi")));
        var client = new OpenAiStyleChatClient(new Factory(handler));
        await Assert.ThrowsAsync<ChatServiceException>(async () =>
        {
            await foreach (var _ in client.StreamAsync(new AiServiceConnection("https://192.168.1.5/v1", "m", Key), Request())) { }
        });
        Assert.Empty(handler.Calls);
    }

    [Theory]
    [InlineData("Answer", "Answer")]
    [InlineData("<think>secret</think>Answer", "Answer")]
    [InlineData("<think>secret</think>\n\nAnswer", "Answer")]
    [InlineData("Before <think>secret</think>after", "Before after")]
    [InlineData("<think>never closes", "")]
    [InlineData("1 < 2 and <b>", "1 < 2 and <b>")]
    public async Task Private_reasoning_never_reaches_the_person(string reply, string shown)
    {
        // One character at a time, the worst way a service could split a tag.
        Assert.Equal(shown, Joined(await Run(new StubHandler(Sse(reply.Select(c => Text(c.ToString())).ToArray())))));
        Assert.Equal(shown, Joined(await Run(new StubHandler(Sse(Text(reply))))));
    }
}
