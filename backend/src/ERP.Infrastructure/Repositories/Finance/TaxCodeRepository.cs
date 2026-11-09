using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class TaxCodeRepository(IRepository<TaxCode> taxCodes) : RepositoryBase<TaxCode>(taxCodes), ITaxCodeRepository
{
    public Task<Dictionary<Guid, TaxCodeLabel>> MapLabelsAsync(CancellationToken ct)
        => Query().Select(t => new TaxCodeLabel(t.Id, t.Code, t.Name, t.Rate)).ToDictionaryAsync(t => t.Id, ct);

    public Task<List<TaxCode>> ListOtherDefaultsForUpdateAsync(Guid exceptId, CancellationToken ct)
        => Tracked(new Specification<TaxCode>(t => t.IsDefault && t.Id != exceptId)).ToListAsync(ct);
    public Task<Dictionary<Guid, TaxCodeRate>> ActiveRatesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<TaxCode>(t => ids.Contains(t.Id) && t.IsActive)).Select(t => new TaxCodeRate(t.Id, t.Rate, t.IsRecoverable)).ToDictionaryAsync(t => t.Id, ct);
}
