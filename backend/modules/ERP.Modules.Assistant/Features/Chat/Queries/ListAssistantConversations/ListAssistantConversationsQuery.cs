using ERP.Application.Common;

namespace ERP.Modules.Assistant.Features;

/// <summary>The caller's 30 most recent chats with the assistant.</summary>
public sealed record ListAssistantConversationsQuery : IQuery<IReadOnlyList<AssistantConversationRow>>;
