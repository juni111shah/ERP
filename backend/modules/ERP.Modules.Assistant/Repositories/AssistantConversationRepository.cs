using ERP.Application.Abstractions;
using ERP.Modules.Assistant.Domain;
using ERP.Modules.Assistant.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assistant.Repositories;

internal sealed class AssistantConversationRepository(IRepository<AssistantConversation> inner) : RepositoryBase<AssistantConversation>(inner), IAssistantConversationRepository
{
    public Task<List<AssistantConversationRow>> ListRecentRowsAsync(Guid userId, CancellationToken ct)
        => Query().Where(c => c.UserId == userId).OrderByDescending(c => c.LastMessageAt).Take(30).Select(c => new AssistantConversationRow(c.Id, c.Title, c.LastMessageAt)).ToListAsync(ct);

    public Task<AssistantConversation?> FindOwnAsync(Guid id, Guid userId, CancellationToken ct)
        => Query().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);

    public Task<AssistantConversation?> FindOwnForUpdateAsync(Guid id, Guid userId, CancellationToken ct)
        => Tracked().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
}
