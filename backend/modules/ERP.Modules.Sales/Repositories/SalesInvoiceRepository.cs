using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Repositories;

internal sealed class SalesInvoiceRepository(IRepository<Invoice> inner) : RepositoryBase<Invoice>(inner), ISalesInvoiceRepository
{
    public Task<Invoice?> FindWithLinesAsync(Guid id, CancellationToken ct)
        => Query(new Specification<Invoice>(x => x.Id == id).Include(x => x.Lines)).FirstOrDefaultAsync(ct);
}
