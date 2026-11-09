using ERP.Application.Abstractions;
using ERP.Modules.Services.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Services.Repositories;

internal sealed class ServiceEngagementRepository(IRepository<ServiceEngagement> inner) : RepositoryBase<ServiceEngagement>(inner), IServiceEngagementRepository
{
    public Task<ServiceEngagement?> FindForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<ServiceEngagement> FirstForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked().FirstAsync(x => x.Id == id, ct);

    public Task<int> CountActiveAsync(Guid legalEntityId, CancellationToken ct)
        => Query().Where(e => e.LegalEntityId == legalEntityId).CountAsync(e => e.Status == EngagementStatus.Active, ct);

    public async Task<decimal> TotalActiveContractValueAsync(Guid legalEntityId, CancellationToken ct)
        => await Query().Where(e => e.LegalEntityId == legalEntityId).Where(e => e.Status == EngagementStatus.Active).SumAsync(e => (decimal?)e.ContractValue, ct) ?? 0m;
}
