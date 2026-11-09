using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class UnitRentRateRepository(IRepository<UnitRentRate> inner) : RepositoryBase<UnitRentRate>(inner), IUnitRentRateRepository
{
    public Task<List<UnitRentRateDto>> ListHistoryAsync(Guid unitId, CancellationToken ct)
        => Query(new Specification<UnitRentRate>(r => r.UnitId == unitId)).OrderByDescending(r => r.EffectiveFrom)
            .Select(r => new UnitRentRateDto(r.Id, r.EffectiveFrom, r.AnnualRent, r.Reason)).ToListAsync(ct);

    public Task<UnitRentRate?> FindForDayForUpdateAsync(Guid unitId, DateOnly day, CancellationToken ct)
        => Tracked(new Specification<UnitRentRate>(r => r.UnitId == unitId && r.EffectiveFrom == day)).FirstOrDefaultAsync(ct);
}
