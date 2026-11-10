using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class PmScheduleRepository(IRepository<PmSchedule> inner) : RepositoryBase<PmSchedule>(inner), IPmScheduleRepository
{
    public Task<List<PmSchedule>> ListActiveForUpdateAsync(CancellationToken ct)
        => Tracked(new Specification<PmSchedule>(s => s.IsActive)).ToListAsync(ct);
}
