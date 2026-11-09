using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectRepository(IRepository<Project> inner) : RepositoryBase<Project>(inner), IProjectRepository
{
    public Task<List<Project>> ListAllAsync(CancellationToken ct) => Query().ToListAsync(ct);

    public Task<List<BillingProjectRow>> ListBillingCandidatesAsync(HashSet<Guid> billable, CancellationToken ct)
        => Query(new Specification<Project>(p => !p.IsTemplate && !p.IsArchived && (p.Status == ProjectStatus.Active || billable.Contains(p.Id))))
            .OrderBy(p => p.Name).Select(p => new BillingProjectRow(p.Id, p.Number, p.Name, p.BillingType, p.Status, p.BudgetAmount, p.ContractValue)).ToListAsync(ct);

    public Task<Dictionary<Guid, string>> MapNamesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Project>(p => ids.Contains(p.Id))).ToDictionaryAsync(p => p.Id, p => p.Name, ct);

    public Task<Dictionary<Guid, string>> MapLabelsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Project>(p => ids.Contains(p.Id))).ToDictionaryAsync(p => p.Id, p => p.Number + " " + p.Name, ct);

    public Task<List<Project>> ListForPortalCustomerAsync(Guid partyId, CancellationToken ct)
        => Query(new Specification<Project>(p => p.CustomerId == partyId && !p.IsTemplate && !p.IsArchived && p.Status != ProjectStatus.Planning && p.Status != ProjectStatus.Cancelled)).ToListAsync(ct);

    public Task<List<Guid>> ListIdsOfCustomerAsync(Guid partyId, CancellationToken ct)
        => Query(new Specification<Project>(p => p.CustomerId == partyId)).Select(p => p.Id).ToListAsync(ct);

    public Task<List<Project>> ListNonTemplatesAsync(Guid? projectId, CancellationToken ct)
        => Query(new Specification<Project>(p => !p.IsTemplate && (projectId == null || p.Id == projectId))).ToListAsync(ct);

    public Task<List<Project>> ListForBudgetReportAsync(CancellationToken ct)
        => Query(new Specification<Project>(p => !p.IsTemplate && !p.IsArchived && p.Status != ProjectStatus.Cancelled)).ToListAsync(ct);

    public Task<List<Project>> ListBillableNonTemplatesAsync(CancellationToken ct)
        => Query(new Specification<Project>(p => !p.IsTemplate && p.BillingType != ProjectBillingType.NonBillable)).ToListAsync(ct);
}
