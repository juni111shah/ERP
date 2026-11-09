using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class LeaveTypeRepository(IRepository<LeaveType> inner) : RepositoryBase<LeaveType>(inner), ILeaveTypeRepository
{
    public Task<List<LeaveType>> ListActiveEncashableAsync(CancellationToken ct)
        => Query(new Specification<LeaveType>(t => t.Encashable && t.IsActive)).ToListAsync(ct);

    public async Task<List<LeaveType>> ListActiveByCodeAsync(CancellationToken ct)
        => (await Query(new Specification<LeaveType>(t => t.IsActive)).ToListAsync(ct)).OrderBy(t => t.Code).ToList();

    public async Task<HashSet<string>> CodesAsync(CancellationToken ct)
        => (await Query().Select(t => t.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<Dictionary<Guid, string>> NamesByIdAsync(CancellationToken ct)
        => (await Query().Select(t => new { t.Id, t.Name }).ToListAsync(ct)).ToDictionary(t => t.Id, t => t.Name);
}
