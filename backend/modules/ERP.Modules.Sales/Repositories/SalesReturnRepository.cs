using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Sales.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Repositories;

internal sealed class SalesReturnRepository(IRepository<SalesReturn> inner) : RepositoryBase<SalesReturn>(inner), ISalesReturnRepository
{
    public async Task<SalesReturn> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<SalesReturn>(r => r.Id == id)).Include(r => r.Lines).FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Sales return '{id}' not found.");
}
