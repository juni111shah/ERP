using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Repositories;

internal sealed class PosInvoiceRepository(IRepository<Invoice> inner) : RepositoryBase<Invoice>(inner), IPosInvoiceRepository
{
    public Task<Invoice?> FindForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<Invoice>(i => i.Id == id)).FirstOrDefaultAsync(ct);
}
