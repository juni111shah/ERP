using ERP.Application.Assistant;
using MediatR;

namespace ERP.Modules.Assistant.Features;

/// <summary>Sends the caller's message to the assistant and streams its reply (and any suggested changes); the chat is saved as it goes.</summary>
public sealed record AskWorkspaceAssistantCommand(WorkspaceMessageInput Input) : IStreamRequest<AssistantEvent>;
