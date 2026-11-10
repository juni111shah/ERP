using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Repositories;

internal sealed class PosTaxCodeRepository(IRepository<TaxCode> inner) : RepositoryBase<TaxCode>(inner), IPosTaxCodeRepository
{
    public Task<Dictionary<Guid, TaxCode>> MapByIdsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<TaxCode>(t => ids.Contains(t.Id))).ToDictionaryAsync(t => t.Id, ct);

    public Task<Dictionary<Guid, decimal>> RatesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<TaxCode>(t => ids.Contains(t.Id))).Select(t => new { t.Id, t.Rate }).ToDictionaryAsync(t => t.Id, t => t.Rate, ct);
}
