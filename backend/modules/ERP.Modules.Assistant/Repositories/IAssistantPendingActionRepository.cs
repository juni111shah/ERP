using ERP.Application.Abstractions;
using ERP.Modules.Assistant.Domain;

namespace ERP.Modules.Assistant.Repositories;

/// <summary>Data access for the changes the assistant suggested.</summary>
public interface IAssistantPendingActionRepository : IRepository<AssistantPendingAction>
{
    /// <summary>The suggestions of a chat, oldest first.</summary>
    Task<List<AssistantPendingAction>> ListForConversationAsync(Guid conversationId, CancellationToken ct);

    /// <summary>Every suggestion of a chat, tracked for changes.</summary>
    Task<List<AssistantPendingAction>> ListForConversationForUpdateAsync(Guid conversationId, CancellationToken ct);

    /// <summary>The person's own suggestion, tracked for changes. Throws NotFoundException.</summary>
    Task<AssistantPendingAction> GetOwnForUpdateAsync(Guid id, Guid userId, CancellationToken ct);
}
