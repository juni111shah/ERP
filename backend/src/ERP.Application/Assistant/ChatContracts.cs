namespace ERP.Application.Assistant;

public enum ChatRole { User, Assistant, Tool }

public sealed record ChatToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>One turn of a conversation, in a form that belongs to no AI service.</summary>
public sealed record ChatMessage(ChatRole Role, string? Text, IReadOnlyList<ChatToolCall>? ToolCalls = null, string? ToolCallId = null);

public sealed record ChatToolDefinition(string Name, string Description, string ParametersJsonSchema);

public sealed record ChatRequest(string System, IReadOnlyList<ChatMessage> Messages, IReadOnlyList<ChatToolDefinition> Tools, int MaxOutputTokens);

public sealed record AiServiceConnection(string BaseUrl, string Model, string ApiKey);

public abstract record ChatStreamEvent;
public sealed record ChatTextDelta(string Text) : ChatStreamEvent;
public sealed record ChatToolCallReady(ChatToolCall Call) : ChatStreamEvent;
/// <summary>Sent at most once. Some services do not report usage, and the runner then estimates it.</summary>
public sealed record ChatUsage(int InputTokens, int OutputTokens) : ChatStreamEvent;

public enum ChatFailureKind { KeyRejected, RateLimited, NotFound, BadRequest, Unavailable, TimedOut, NotAllowed }

/// <summary>The service could not answer. The message comes from a fixed list, so it never holds a key or the service's own reply.</summary>
public sealed class ChatServiceException(ChatFailureKind kind, string message, TimeSpan? retryAfter = null) : Exception(message)
{
    public ChatFailureKind Kind { get; } = kind;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

/// <summary>Talks to an AI service (implemented in Infrastructure). Throws <see cref="ChatServiceException"/> when the service cannot answer.</summary>
public interface IChatModelClient
{
    IAsyncEnumerable<ChatStreamEvent> StreamAsync(AiServiceConnection connection, ChatRequest request, CancellationToken ct = default);
}

/// <summary>Something the assistant may look up while answering. Each tool checks that the caller may see what it returns.</summary>
public interface IAssistantTool
{
    ChatToolDefinition Definition { get; }
    /// <summary>What the person sees while it runs, such as "Checking the plans".</summary>
    string StatusText { get; }
    Task<string> RunAsync(string argumentsJson, CancellationToken ct);
}

public enum AssistantBot { Brand, Workspace }

/// <summary>What a reply is made of, in the order it happens.</summary>
public abstract record AssistantEvent;
public sealed record AssistantStatus(string Text) : AssistantEvent;
public sealed record AssistantDelta(string Text) : AssistantEvent;
/// <summary>Which service answered. For the founder's console only: never pass this to visitors or clients.</summary>
public sealed record AssistantServiceUsed(string Name) : AssistantEvent;
public sealed record AssistantDone(int InputTokens, int OutputTokens) : AssistantEvent;
public sealed record AssistantError(string Code, string Message) : AssistantEvent;
/// <summary>Something beyond text that a particular assistant adds, such as a signed session or price cards. The name becomes the event name.</summary>
public sealed record AssistantData(string Name, object Payload) : AssistantEvent;

public sealed record AssistantTurn(AssistantBot Bot, string System, IReadOnlyList<ChatMessage> History, IReadOnlyList<IAssistantTool> Tools, int MaxOutputTokens = 1024);

public sealed record AssistantUsageRow(AssistantBot Bot, Guid ProviderId, string ProviderName, int Requests, int Failures, long InputTokens, long OutputTokens, string? LastFailure);

/// <summary>Counts what each service used per day. Updates are atomic, because several replies can finish at once.</summary>
public interface IAssistantUsageStore
{
    Task<long> TokensOnAsync(DateOnly day, AssistantBot bot, CancellationToken ct = default);
    Task RecordAsync(DateOnly day, AssistantBot bot, Guid providerId, string providerName, int inputTokens, int outputTokens, string? failure, CancellationToken ct = default);
    Task<IReadOnlyList<AssistantUsageRow>> ListAsync(DateOnly day, CancellationToken ct = default);
}
