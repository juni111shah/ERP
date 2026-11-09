using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for employee tasks (onboarding, offboarding, one-off).</summary>
public interface IEmployeeTaskRepository : IRepository<EmployeeTask>
{
    /// <summary>The titles of an employee's tasks of one kind (compared ignoring case).</summary>
    Task<HashSet<string>> TitlesOfKindAsync(Guid employeeId, ChecklistKind kind, CancellationToken ct);

    /// <summary>An employee's tasks, open ones first, by due date.</summary>
    Task<List<EmployeeTask>> ListForEmployeeAsync(Guid employeeId, CancellationToken ct);

    /// <summary>Every open task, soonest due first (no due date last).</summary>
    Task<List<EmployeeTask>> ListOpenByDueDateAsync(CancellationToken ct);
}
