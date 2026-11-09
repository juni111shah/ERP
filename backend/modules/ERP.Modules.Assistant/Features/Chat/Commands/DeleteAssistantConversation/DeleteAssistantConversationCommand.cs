using ERP.Application.Common;

namespace ERP.Modules.Assistant.Features;

/// <summary>Deletes one of the caller's chats with its messages and suggested changes.</summary>
public sealed record DeleteAssistantConversationCommand(Guid Id) : ICommand;
