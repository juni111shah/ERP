using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for the employee history.</summary>
public interface IEmployeeHistoryEntryRepository : IRepository<EmployeeHistoryEntry>
{
    /// <summary>An employee's history entries, newest effective date first (then newest created).</summary>
    Task<List<EmployeeHistoryEntry>> ListForEmployeeNewestFirstAsync(Guid employeeId, CancellationToken ct);

    /// <summary>The note most recently added to an employee's history.</summary>
    Task<EmployeeHistoryEntry> GetLatestNoteAsync(Guid employeeId, CancellationToken ct);
}
