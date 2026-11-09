using ERP.Application.Abstractions;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Platform;

internal sealed class AiProviderRepository(IRepository<AiProvider> providers) : RepositoryBase<AiProvider>(providers), IAiProviderRepository
{
    public Task<List<AiProvider>> ListInOrderAsync(CancellationToken ct) => Query().OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt).ToListAsync(ct);

    public Task<int?> LastSortOrderAsync(CancellationToken ct) => Query().OrderByDescending(p => p.SortOrder).Select(p => (int?)p.SortOrder).FirstOrDefaultAsync(ct);

    public Task<List<AiProvider>> ListForUpdateAsync(CancellationToken ct) => Tracked().ToListAsync(ct);
}
