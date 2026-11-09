using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Assistant.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assistant.Repositories;

internal sealed class AssistantPendingActionRepository(IRepository<AssistantPendingAction> inner) : RepositoryBase<AssistantPendingAction>(inner), IAssistantPendingActionRepository
{
    public Task<List<AssistantPendingAction>> ListForConversationAsync(Guid conversationId, CancellationToken ct)
        => Query().Where(a => a.ConversationId == conversationId).OrderBy(a => a.CreatedAt).ToListAsync(ct);

    public Task<List<AssistantPendingAction>> ListForConversationForUpdateAsync(Guid conversationId, CancellationToken ct)
        => Tracked().Where(a => a.ConversationId == conversationId).ToListAsync(ct);

    public async Task<AssistantPendingAction> GetOwnForUpdateAsync(Guid id, Guid userId, CancellationToken ct)
        => await Tracked().FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct) ?? throw new NotFoundException();
}
