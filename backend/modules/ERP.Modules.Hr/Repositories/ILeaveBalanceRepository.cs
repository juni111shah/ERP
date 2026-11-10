using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for leave balances.</summary>
public interface ILeaveBalanceRepository : IRepository<LeaveBalance>
{
    /// <summary>The balance of one employee, leave type and year, read-only; null when there is none yet.</summary>
    Task<LeaveBalance?> FindForYearAsync(Guid employeeId, Guid leaveTypeId, int year, CancellationToken ct);

    /// <summary>The balance of one employee, leave type and year, tracked for changes; null when there is none yet.</summary>
    Task<LeaveBalance?> FindForYearForUpdateAsync(Guid employeeId, Guid leaveTypeId, int year, CancellationToken ct);

    /// <summary>The year's balances of the employees, by employee and leave type.</summary>
    Task<Dictionary<(Guid EmployeeId, Guid LeaveTypeId), LeaveBalance>> MapForYearAsync(int year, List<Guid> employeeIds, CancellationToken ct);
}
