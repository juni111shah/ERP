using ERP.Application.Abstractions;
using ERP.Modules.Purchasing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Repositories;

internal sealed class PurchaseRequisitionRepository(IRepository<PurchaseRequisition> inner) : RepositoryBase<PurchaseRequisition>(inner), IPurchaseRequisitionRepository
{
    public Task<PurchaseRequisition?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<PurchaseRequisition>(x => x.Id == id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct);

    public Task<PurchaseRequisition?> FindConvertedToOrderForUpdateAsync(Guid id, Guid orderId, CancellationToken ct)
        => Tracked(new Specification<PurchaseRequisition>(r => r.Id == id && r.ConvertedPurchaseOrderId == orderId)).FirstOrDefaultAsync(ct);
}
