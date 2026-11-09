using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class EmployeeTaskRepository(IRepository<EmployeeTask> inner) : RepositoryBase<EmployeeTask>(inner), IEmployeeTaskRepository
{
    public async Task<HashSet<string>> TitlesOfKindAsync(Guid employeeId, ChecklistKind kind, CancellationToken ct)
        => (await Query(new Specification<EmployeeTask>(x => x.EmployeeId == employeeId && x.Kind == kind)).Select(x => x.Title).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<List<EmployeeTask>> ListForEmployeeAsync(Guid employeeId, CancellationToken ct)
        => (await Query(new Specification<EmployeeTask>(x => x.EmployeeId == employeeId)).ToListAsync(ct)).OrderBy(x => x.IsDone).ThenBy(x => x.DueDate).ToList();

    public async Task<List<EmployeeTask>> ListOpenByDueDateAsync(CancellationToken ct)
        => (await Query(new Specification<EmployeeTask>(x => !x.IsDone)).ToListAsync(ct)).OrderBy(x => x.DueDate ?? DateOnly.MaxValue).ToList();
}
