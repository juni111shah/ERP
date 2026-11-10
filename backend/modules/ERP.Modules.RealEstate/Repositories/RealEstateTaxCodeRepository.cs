using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class RealEstateTaxCodeRepository(IRepository<TaxCode> inner) : RepositoryBase<TaxCode>(inner), IRealEstateTaxCodeRepository
{
    public Task<Guid?> FindActiveDefaultIdAsync(CancellationToken ct)
        => Query(new Specification<TaxCode>(t => t.IsDefault && t.IsActive)).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
}
