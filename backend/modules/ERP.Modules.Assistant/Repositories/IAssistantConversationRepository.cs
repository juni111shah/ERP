using ERP.Application.Abstractions;
using ERP.Modules.Assistant.Domain;
using ERP.Modules.Assistant.Features;

namespace ERP.Modules.Assistant.Repositories;

/// <summary>Data access for a person's assistant chats.</summary>
public interface IAssistantConversationRepository : IRepository<AssistantConversation>
{
    /// <summary>The person's 30 most recent chats, latest first.</summary>
    Task<List<AssistantConversationRow>> ListRecentRowsAsync(Guid userId, CancellationToken ct);

    /// <summary>The person's own chat, read-only, or null.</summary>
    Task<AssistantConversation?> FindOwnAsync(Guid id, Guid userId, CancellationToken ct);

    /// <summary>The person's own chat, tracked for changes, or null.</summary>
    Task<AssistantConversation?> FindOwnForUpdateAsync(Guid id, Guid userId, CancellationToken ct);
}
