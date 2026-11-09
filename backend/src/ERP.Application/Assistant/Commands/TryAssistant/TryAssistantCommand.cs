using MediatR;

namespace ERP.Application.Assistant;

/// <summary>The founder tries the connected services from the console, whether or not the assistants are switched on yet.</summary>
public sealed record TryAssistantCommand(AssistantTryInput Input) : IStreamRequest<AssistantEvent>;
