using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Disconnects an AI service and deletes its stored key (owner only).</summary>
public sealed record DeleteAiProviderCommand(Guid Id) : ICommand<AssistantConsoleView>;
