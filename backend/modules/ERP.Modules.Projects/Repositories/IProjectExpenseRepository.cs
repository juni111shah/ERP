using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for project expenses. Implemented in ProjectExpenseRepository.cs.</summary>
public interface IProjectExpenseRepository : IRepository<ProjectExpense>
{
    /// <summary>How many expenses of the project are ready to bill: billable, approved, not billed, not written off.</summary>
    Task<int> CountReadyToBillAsync(Guid projectId, CancellationToken ct);

    /// <summary>The expenses with the given ids, tracked for changes.</summary>
    Task<List<ProjectExpense>> ListByIdsForUpdateAsync(IReadOnlyList<Guid> ids, CancellationToken ct);

    /// <summary>The project's expenses that are ready to bill, by date, read-only.</summary>
    Task<List<ProjectExpense>> ListReadyToBillAsync(Guid projectId, CancellationToken ct);

    /// <summary>The project's expenses that are ready to bill, tracked for changes.</summary>
    Task<List<ProjectExpense>> ListReadyToBillForUpdateAsync(Guid projectId, CancellationToken ct);

    /// <summary>The project's expenses that are ready to bill (only <paramref name="onlyIds"/> when set), by date, tracked for changes.</summary>
    Task<List<ProjectExpense>> ListChosenReadyToBillForUpdateAsync(Guid projectId, IReadOnlyList<Guid>? onlyIds, CancellationToken ct);

    /// <summary>Every expense that is ready to bill, read-only.</summary>
    Task<List<ProjectExpense>> ListAllReadyToBillAsync(CancellationToken ct);

    /// <summary>The project's approved expenses that are not in the ledger yet (and not written off), tracked for changes.</summary>
    Task<List<ProjectExpense>> ListUnpostedCostForUpdateAsync(Guid projectId, CancellationToken ct);

    /// <summary>Every expense of the project, read-only.</summary>
    Task<List<ProjectExpense>> ListOfProjectAsync(Guid projectId, CancellationToken ct);

    /// <summary>The project's approved expenses, read-only.</summary>
    Task<List<ProjectExpense>> ListApprovedOfProjectAsync(Guid projectId, CancellationToken ct);

    /// <summary>The approved expenses of the projects, read-only.</summary>
    Task<List<ProjectExpense>> ListApprovedOfProjectsAsync(List<Guid> projectIds, CancellationToken ct);

    /// <summary>The approved expenses of the projects dated in the (open-ended) period, read-only.</summary>
    Task<List<ProjectExpense>> ListApprovedOfProjectsBetweenAsync(List<Guid> projectIds, DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>The expenses of the projects that are ready to bill, read-only.</summary>
    Task<List<ProjectExpense>> ListReadyToBillOfProjectsAsync(List<Guid> projectIds, CancellationToken ct);
}
