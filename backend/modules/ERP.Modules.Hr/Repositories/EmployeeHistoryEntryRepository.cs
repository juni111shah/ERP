using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class EmployeeHistoryEntryRepository(IRepository<EmployeeHistoryEntry> inner) : RepositoryBase<EmployeeHistoryEntry>(inner), IEmployeeHistoryEntryRepository
{
    public async Task<List<EmployeeHistoryEntry>> ListForEmployeeNewestFirstAsync(Guid employeeId, CancellationToken ct)
    {
        var rows = await Query(new Specification<EmployeeHistoryEntry>(h => h.EmployeeId == employeeId)).ToListAsync(ct);
        return rows.OrderByDescending(h => h.EffectiveDate).ThenByDescending(h => h.CreatedAt).ToList();
    }

    public async Task<EmployeeHistoryEntry> GetLatestNoteAsync(Guid employeeId, CancellationToken ct)
        => (await Query(new Specification<EmployeeHistoryEntry>(h => h.EmployeeId == employeeId && h.Kind == HistoryKind.Note)).ToListAsync(ct)).OrderByDescending(h => h.CreatedAt).First();
}
