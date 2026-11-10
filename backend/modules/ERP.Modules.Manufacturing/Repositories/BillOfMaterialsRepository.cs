using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Manufacturing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Manufacturing.Repositories;

internal sealed class BillOfMaterialsRepository(IRepository<BillOfMaterials> inner) : RepositoryBase<BillOfMaterials>(inner), IBillOfMaterialsRepository
{
    public Task<BillOfMaterials?> FindWithComponentsAsync(Guid id, CancellationToken ct)
        => Query(new Specification<BillOfMaterials>(b => b.Id == id))
            .Include(b => b.Components)
            .FirstOrDefaultAsync(ct);

    public async Task<BillOfMaterials> GetWithComponentsAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<BillOfMaterials>(b => b.Id == id))
            .Include(b => b.Components)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"BOM {id} not found.");

    public async Task<BillOfMaterials> GetWithComponentsForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<BillOfMaterials>(b => b.Id == id).Include(b => b.Components))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"BOM {id} not found.");

    public Task<List<BillOfMaterials>> ListOtherActiveWithComponentsAsync(Guid legalEntityId, Guid? excludeBomId, CancellationToken ct)
        => Query(new Specification<BillOfMaterials>(b => b.LegalEntityId == legalEntityId && b.IsActive && b.Id != excludeBomId))
            .Include(b => b.Components).ToListAsync(ct);

    public Task<List<BillOfMaterials>> ListActiveOrNamedWithComponentsAsync(Guid legalEntityId, List<Guid> bomIds, CancellationToken ct)
        => Query(new Specification<BillOfMaterials>(b => b.LegalEntityId == legalEntityId && (b.IsActive || bomIds.Contains(b.Id))))
            .Include(b => b.Components).ToListAsync(ct);

    public Task<BillOfMaterials?> FindActiveForItemWithComponentsAsync(Guid legalEntityId, Guid itemId, CancellationToken ct)
        => Query(new Specification<BillOfMaterials>(b =>
            b.LegalEntityId == legalEntityId && b.ProducedItemId == itemId && b.IsActive))
            .Include(b => b.Components)
            .FirstOrDefaultAsync(ct);

    public Task<int> CountActiveAsync(CancellationToken ct)
        => Query(new Specification<BillOfMaterials>(b => b.IsActive)).CountAsync(ct);
}
