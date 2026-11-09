using ERP.Application.Abstractions;
using ERP.Modules.Assistant.Domain;
using ERP.Modules.Assistant.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assistant.Repositories;

internal sealed class AssistantChatMessageRepository(IRepository<AssistantChatMessage> inner) : RepositoryBase<AssistantChatMessage>(inner), IAssistantChatMessageRepository
{
    public Task<List<AssistantMessageRow>> ListRowsAsync(Guid conversationId, CancellationToken ct)
        => Query().Where(m => m.ConversationId == conversationId).OrderBy(m => m.At).Select(m => new AssistantMessageRow(m.At, m.FromUser, m.Text)).ToListAsync(ct);

    public Task<List<AssistantChatMessage>> ListLatestAsync(Guid conversationId, int take, CancellationToken ct)
        => Query().Where(m => m.ConversationId == conversationId).OrderByDescending(m => m.At).Take(take).ToListAsync(ct);

    public Task<List<AssistantChatMessage>> ListForConversationForUpdateAsync(Guid conversationId, CancellationToken ct)
        => Tracked().Where(m => m.ConversationId == conversationId).ToListAsync(ct);
}
