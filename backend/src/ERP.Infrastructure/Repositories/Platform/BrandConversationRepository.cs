using ERP.Application.Abstractions;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Platform;

internal sealed class BrandConversationRepository(IRepository<BrandConversation> conversations) : RepositoryBase<BrandConversation>(conversations), IBrandConversationRepository
{
    public Task<BrandConversation?> FindForUpdateAsync(Guid id, CancellationToken ct) => Tracked().FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<(List<BrandConversation> Rows, int Total)> ListPageAsync(int page, int size, CancellationToken ct)
    {
        var query = Query();
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(c => c.NeedsReview).ThenByDescending(c => c.LastMessageAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return (rows, total);
    }
}
