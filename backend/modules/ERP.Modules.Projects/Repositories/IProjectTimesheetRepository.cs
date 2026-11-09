using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for timesheet entries. Implemented in ProjectTimesheetRepository.cs.</summary>
public interface IProjectTimesheetRepository : IRepository<ProjectTimesheet>
{
    /// <summary>How many entries of the project are ready to bill: billable, approved, not billed, not written off.</summary>
    Task<int> CountReadyToBillAsync(Guid projectId, CancellationToken ct);

    /// <summary>The entries with the given ids, tracked for changes.</summary>
    Task<List<ProjectTimesheet>> ListByIdsForUpdateAsync(IReadOnlyList<Guid> ids, CancellationToken ct);

    /// <summary>The hours a sign-in account has logged on the day, leaving out <paramref name="exceptId"/>.</summary>
    Task<decimal> HoursOnDayOfUserAsync(DateOnly date, Guid? exceptId, Guid userId, CancellationToken ct);

    /// <summary>The hours a typed name (no sign-in account) has logged on the day, leaving out <paramref name="exceptId"/>.</summary>
    Task<decimal> HoursOnDayOfNameAsync(DateOnly date, Guid? exceptId, string name, CancellationToken ct);

    /// <summary>The project's entries that are ready to bill, by date, read-only.</summary>
    Task<List<ProjectTimesheet>> ListReadyToBillAsync(Guid projectId, CancellationToken ct);

    /// <summary>The project's entries that are ready to bill, tracked for changes.</summary>
    Task<List<ProjectTimesheet>> ListReadyToBillForUpdateAsync(Guid projectId, CancellationToken ct);

    /// <summary>The project's entries that are ready to bill (only <paramref name="onlyIds"/> when set), by date, tracked for changes.</summary>
    Task<List<ProjectTimesheet>> ListChosenReadyToBillForUpdateAsync(Guid projectId, IReadOnlyList<Guid>? onlyIds, CancellationToken ct);

    /// <summary>Every billable entry that is neither billed nor written off, with what the billing overview needs.</summary>
    Task<List<UnbilledTimeRow>> ListUnbilledAsync(CancellationToken ct);

    /// <summary>The project's approved entries with a cost that is not in the ledger yet (and not written off), tracked for changes.</summary>
    Task<List<ProjectTimesheet>> ListUnpostedCostForUpdateAsync(Guid projectId, CancellationToken ct);

    /// <summary>Every entry of the project, read-only.</summary>
    Task<List<ProjectTimesheet>> ListOfProjectAsync(Guid projectId, CancellationToken ct);

    /// <summary>The project's entries that were not rejected, read-only.</summary>
    Task<List<ProjectTimesheet>> ListNotRejectedOfProjectAsync(Guid projectId, CancellationToken ct);

    /// <summary>The entries of the projects that were not rejected, read-only.</summary>
    Task<List<ProjectTimesheet>> ListNotRejectedOfProjectsAsync(List<Guid> projectIds, CancellationToken ct);

    /// <summary>The entries of the projects that were not rejected, dated in the (open-ended) period, read-only.</summary>
    Task<List<ProjectTimesheet>> ListNotRejectedOfProjectsBetweenAsync(List<Guid> projectIds, DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>Every entry dated in the period that was not rejected, read-only.</summary>
    Task<List<ProjectTimesheet>> ListNotRejectedBetweenAsync(DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>The entries dated in the period that were not rejected, of one project when set, read-only.</summary>
    Task<List<ProjectTimesheet>> ListNotRejectedBetweenAsync(DateOnly from, DateOnly to, Guid? projectId, CancellationToken ct);

    /// <summary>The entries of the projects that are ready to bill, read-only.</summary>
    Task<List<ProjectTimesheet>> ListReadyToBillOfProjectsAsync(List<Guid> projectIds, CancellationToken ct);
}

/// <summary>A billable entry that is not billed yet.</summary>
public sealed record UnbilledTimeRow(Guid ProjectId, decimal Hours, decimal TotalAmount, TimesheetStatus ApprovalStatus);
