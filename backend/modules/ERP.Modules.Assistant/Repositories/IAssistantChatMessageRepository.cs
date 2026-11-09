using ERP.Application.Abstractions;
using ERP.Modules.Assistant.Domain;
using ERP.Modules.Assistant.Features;

namespace ERP.Modules.Assistant.Repositories;

/// <summary>Data access for the messages of assistant chats.</summary>
public interface IAssistantChatMessageRepository : IRepository<AssistantChatMessage>
{
    /// <summary>The messages of a chat, oldest first.</summary>
    Task<List<AssistantMessageRow>> ListRowsAsync(Guid conversationId, CancellationToken ct);

    /// <summary>The latest <paramref name="take"/> messages of a chat, latest first.</summary>
    Task<List<AssistantChatMessage>> ListLatestAsync(Guid conversationId, int take, CancellationToken ct);

    /// <summary>Every message of a chat, tracked for changes.</summary>
    Task<List<AssistantChatMessage>> ListForConversationForUpdateAsync(Guid conversationId, CancellationToken ct);
}
