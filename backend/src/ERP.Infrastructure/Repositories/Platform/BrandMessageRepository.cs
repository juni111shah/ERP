using ERP.Application.Abstractions;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Platform;

internal sealed class BrandMessageRepository(IRepository<BrandMessage> messages) : RepositoryBase<BrandMessage>(messages), IBrandMessageRepository
{
    public Task<List<BrandMessage>> ListLatestAsync(Guid conversationId, int take, CancellationToken ct)
        => Query().Where(m => m.ConversationId == conversationId).OrderByDescending(m => m.At).Take(take).ToListAsync(ct);

    public async Task<Dictionary<Guid, string>> FirstVisitorMessagesAsync(IReadOnlyCollection<Guid> conversationIds, CancellationToken ct)
        => (await Query().Where(m => conversationIds.Contains(m.ConversationId) && m.FromVisitor).OrderBy(m => m.At).Select(m => new { m.ConversationId, m.Text }).ToListAsync(ct))
            .GroupBy(m => m.ConversationId).ToDictionary(g => g.Key, g => g.First().Text);

    public Task<List<BrandMessageRow>> ListRowsAsync(Guid conversationId, CancellationToken ct)
        => Query().Where(m => m.ConversationId == conversationId).OrderBy(m => m.At).Select(m => new BrandMessageRow(m.At, m.FromVisitor, m.Text)).ToListAsync(ct);
}
