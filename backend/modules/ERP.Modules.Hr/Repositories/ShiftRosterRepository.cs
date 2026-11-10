using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class ShiftRosterRepository(IRepository<ShiftRoster> inner) : RepositoryBase<ShiftRoster>(inner), IShiftRosterRepository
{
    public Task<List<ShiftRoster>> ListCoveringAsync(Guid employeeId, DateOnly date, CancellationToken ct)
        => Query(new Specification<ShiftRoster>(r => r.EmployeeId == employeeId && r.FromDate <= date && r.ToDate >= date)).ToListAsync(ct);

    public Task<List<ShiftRoster>> ListOverlappingAsync(DateOnly from, DateOnly to, CancellationToken ct)
        => Query(new Specification<ShiftRoster>(r => r.FromDate <= to && r.ToDate >= from)).ToListAsync(ct);
}
