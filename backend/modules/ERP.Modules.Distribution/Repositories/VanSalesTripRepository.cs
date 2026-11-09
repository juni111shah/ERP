using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Distribution.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Distribution.Repositories;

internal sealed class VanSalesTripRepository(IRepository<VanSalesTrip> inner) : RepositoryBase<VanSalesTrip>(inner), IVanSalesTripRepository
{
    public async Task<VanSalesTrip> GetWithItemsForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<VanSalesTrip>(t => t.Id == id))
            .Include(t => t.InventoryItems)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Trip {id} not found.");

    public Task<VanSalesTrip?> FindWithItemsAsync(Guid id, CancellationToken ct)
        => Query(new Specification<VanSalesTrip>(t => t.Id == id))
            .Include(t => t.InventoryItems)
            .FirstOrDefaultAsync(ct);

    public Task<int> CountInProgressDepartedOnAsync(DateOnly date, CancellationToken ct)
        => Query(new Specification<VanSalesTrip>(t =>
            t.Status == TripStatus.InProgress && t.DepartureDate == date)).CountAsync(ct);

    public Task<List<VanSalesTrip>> ListCompletedDepartedSinceAsync(DateOnly from, CancellationToken ct)
        => Query(new Specification<VanSalesTrip>(t =>
            t.Status == TripStatus.Completed && t.DepartureDate >= from)).ToListAsync(ct);
}
