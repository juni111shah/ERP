using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectExpenseRepository(IRepository<ProjectExpense> inner) : RepositoryBase<ProjectExpense>(inner), IProjectExpenseRepository
{
    public Task<int> CountReadyToBillAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectExpense>(e => e.ProjectId == projectId && e.IsBillable && !e.IsBilled && !e.WrittenOff && e.Status == ExpenseStatus.Approved)).CountAsync(ct);

    public Task<List<ProjectExpense>> ListByIdsForUpdateAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
        => Tracked(new Specification<ProjectExpense>(e => ids.Contains(e.Id))).ToListAsync(ct);

    public Task<List<ProjectExpense>> ListReadyToBillAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectExpense>(x => x.ProjectId == projectId && x.IsBillable && !x.IsBilled && !x.WrittenOff && x.Status == ExpenseStatus.Approved)).OrderBy(x => x.Date).ToListAsync(ct);

    public Task<List<ProjectExpense>> ListReadyToBillForUpdateAsync(Guid projectId, CancellationToken ct)
        => Tracked(new Specification<ProjectExpense>(x => x.ProjectId == projectId && x.IsBillable && !x.IsBilled && !x.WrittenOff && x.Status == ExpenseStatus.Approved)).ToListAsync(ct);

    public Task<List<ProjectExpense>> ListChosenReadyToBillForUpdateAsync(Guid projectId, IReadOnlyList<Guid>? onlyIds, CancellationToken ct)
    {
        var exQuery = Tracked(new Specification<ProjectExpense>(x => x.ProjectId == projectId && x.IsBillable && !x.IsBilled && !x.WrittenOff && x.Status == ExpenseStatus.Approved));
        if (onlyIds is not null) { var ids = onlyIds.ToHashSet(); exQuery = exQuery.Where(x => ids.Contains(x.Id)); }
        return exQuery.OrderBy(x => x.Date).ToListAsync(ct);
    }

    public Task<List<ProjectExpense>> ListAllReadyToBillAsync(CancellationToken ct)
        => Query(new Specification<ProjectExpense>(x => x.IsBillable && !x.IsBilled && !x.WrittenOff && x.Status == ExpenseStatus.Approved)).ToListAsync(ct);

    public Task<List<ProjectExpense>> ListUnpostedCostForUpdateAsync(Guid projectId, CancellationToken ct)
        => Tracked(new Specification<ProjectExpense>(e => e.ProjectId == projectId && e.CostJournalId == null && e.Amount > 0 && e.Status == ExpenseStatus.Approved && !e.WrittenOff)).ToListAsync(ct);

    public Task<List<ProjectExpense>> ListOfProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectExpense>(x => x.ProjectId == projectId)).ToListAsync(ct);

    public Task<List<ProjectExpense>> ListApprovedOfProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectExpense>(x => x.ProjectId == projectId && x.Status == ExpenseStatus.Approved)).ToListAsync(ct);

    public Task<List<ProjectExpense>> ListApprovedOfProjectsAsync(List<Guid> projectIds, CancellationToken ct)
        => Query(new Specification<ProjectExpense>(e => projectIds.Contains(e.ProjectId) && e.Status == ExpenseStatus.Approved)).ToListAsync(ct);

    public Task<List<ProjectExpense>> ListApprovedOfProjectsBetweenAsync(List<Guid> projectIds, DateOnly? from, DateOnly? to, CancellationToken ct)
        => Query(new Specification<ProjectExpense>(e => projectIds.Contains(e.ProjectId) && e.Status == ExpenseStatus.Approved && (from == null || e.Date >= from) && (to == null || e.Date <= to))).ToListAsync(ct);

    public Task<List<ProjectExpense>> ListReadyToBillOfProjectsAsync(List<Guid> projectIds, CancellationToken ct)
        => Query(new Specification<ProjectExpense>(e => projectIds.Contains(e.ProjectId) && e.IsBillable && !e.IsBilled && !e.WrittenOff && e.Status == ExpenseStatus.Approved)).ToListAsync(ct);
}
