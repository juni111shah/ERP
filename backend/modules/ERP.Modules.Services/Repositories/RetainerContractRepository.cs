using ERP.Application.Abstractions;
using ERP.Modules.Services.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Services.Repositories;

internal sealed class RetainerContractRepository(IRepository<RetainerContract> inner) : RepositoryBase<RetainerContract>(inner), IRetainerContractRepository
{
    public Task<RetainerContract?> FindForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<RetainerContract>> ListActiveAsync(Guid legalEntityId, CancellationToken ct)
        => Query().Where(r => r.LegalEntityId == legalEntityId && r.Status == RetainerStatus.Active).ToListAsync(ct);
}
