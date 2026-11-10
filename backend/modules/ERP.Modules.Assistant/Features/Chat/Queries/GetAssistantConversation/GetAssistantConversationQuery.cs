using ERP.Application.Common;

namespace ERP.Modules.Assistant.Features;

/// <summary>One of the caller's chats with its messages and suggested changes.</summary>
public sealed record GetAssistantConversationQuery(Guid Id) : IQuery<AssistantConversationDetail>;
