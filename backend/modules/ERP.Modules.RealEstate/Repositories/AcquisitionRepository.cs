using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class AcquisitionRepository(IRepository<Acquisition> inner) : RepositoryBase<Acquisition>(inner), IAcquisitionRepository
{
    public Task<List<AcquisitionAmounts>> ListLiveAmountsAsync(CancellationToken ct)
        => Query(new Specification<Acquisition>(a => a.Status != AcquisitionStatus.Cancelled)).Select(a => new AcquisitionAmounts(a.Id, a.PurchasePrice, a.DownPayment)).ToListAsync(ct);
}
