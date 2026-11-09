using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for projects. Implemented in ProjectRepository.cs.</summary>
public interface IProjectRepository : IRepository<Project>
{
    /// <summary>Every project, read-only.</summary>
    Task<List<Project>> ListAllAsync(CancellationToken ct);

    /// <summary>The live projects (not templates, not archived) that are active or have something to bill, by name.</summary>
    Task<List<BillingProjectRow>> ListBillingCandidatesAsync(HashSet<Guid> billable, CancellationToken ct);

    /// <summary>The names of the projects, by id.</summary>
    Task<Dictionary<Guid, string>> MapNamesAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>"Number Name" of the projects, by id.</summary>
    Task<Dictionary<Guid, string>> MapLabelsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The projects a portal customer may see: theirs, not templates or archived, past planning and not cancelled.</summary>
    Task<List<Project>> ListForPortalCustomerAsync(Guid partyId, CancellationToken ct);

    /// <summary>The ids of every project of the customer.</summary>
    Task<List<Guid>> ListIdsOfCustomerAsync(Guid partyId, CancellationToken ct);

    /// <summary>Every project that is not a template, or only <paramref name="projectId"/> when set.</summary>
    Task<List<Project>> ListNonTemplatesAsync(Guid? projectId, CancellationToken ct);

    /// <summary>The projects the budget report covers: not templates, not archived, not cancelled.</summary>
    Task<List<Project>> ListForBudgetReportAsync(CancellationToken ct);

    /// <summary>The projects that are neither templates nor non-billable.</summary>
    Task<List<Project>> ListBillableNonTemplatesAsync(CancellationToken ct);
}

/// <summary>What the billing overview needs of one project.</summary>
public sealed record BillingProjectRow(Guid Id, string Number, string Name, ProjectBillingType BillingType, ProjectStatus Status, decimal BudgetAmount, decimal? ContractValue);
