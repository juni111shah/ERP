using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for leave requests.</summary>
public interface ILeaveRequestRepository : IRepository<LeaveRequest>
{
    /// <summary>Which of the employees have approved leave covering <paramref name="today"/>.</summary>
    Task<HashSet<Guid>> ListEmployeesOnLeaveAsync(DateOnly today, List<Guid> employeeIds, CancellationToken ct);

    /// <summary>An employee's draft, submitted and approved requests starting after <paramref name="after"/>, tracked for changes.</summary>
    Task<List<LeaveRequest>> ListOpenStartingAfterForUpdateAsync(Guid employeeId, DateOnly after, CancellationToken ct);

    /// <summary>The days of an employee's submitted requests of a type starting in <paramref name="year"/>, leaving out <paramref name="excludeRequestId"/>.</summary>
    Task<decimal> PendingDaysAsync(Guid employeeId, Guid leaveTypeId, Guid excludeRequestId, int year, CancellationToken ct);

    /// <summary>The days of the employees' submitted requests starting from <paramref name="from"/> to <paramref name="to"/>, per employee and leave type.</summary>
    Task<Dictionary<(Guid EmployeeId, Guid LeaveTypeId), decimal>> PendingDaysByEmployeeAndTypeAsync(DateOnly from, DateOnly to, List<Guid> employeeIds, CancellationToken ct);

    /// <summary>A page of one employee's requests.</summary>
    Task<PagedResult<LeaveRequestRow>> PageOfEmployeeAsync(PagedQuery paging, Guid employeeId, CancellationToken ct);

    /// <summary>A page of the employees' requests, of one status or (null) all.</summary>
    Task<PagedResult<LeaveRequestRow>> PageOfEmployeesAsync(PagedQuery paging, List<Guid> employeeIds, LeaveStatus? status, CancellationToken ct);

    /// <summary>Type and days of the approved requests (of one entity, or all) starting from <paramref name="from"/> to <paramref name="to"/>.</summary>
    Task<List<LeaveTypeDays>> ListApprovedDaysAsync(Guid? legalEntityId, DateOnly from, DateOnly to, CancellationToken ct);
}

/// <summary>The leave type and days of one request.</summary>
public sealed record LeaveTypeDays(Guid LeaveTypeId, decimal Days);
