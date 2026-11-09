using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class LeaveRequestRepository(IRepository<LeaveRequest> inner) : RepositoryBase<LeaveRequest>(inner), ILeaveRequestRepository
{
    public async Task<HashSet<Guid>> ListEmployeesOnLeaveAsync(DateOnly today, List<Guid> employeeIds, CancellationToken ct)
        => (await Query(new Specification<LeaveRequest>(r => r.Status == LeaveStatus.Approved && r.FromDate <= today && r.ToDate >= today && employeeIds.Contains(r.EmployeeId)))
            .Select(r => r.EmployeeId).ToListAsync(ct)).ToHashSet();

    public Task<List<LeaveRequest>> ListOpenStartingAfterForUpdateAsync(Guid employeeId, DateOnly after, CancellationToken ct)
        => Tracked(new Specification<LeaveRequest>(r => r.EmployeeId == employeeId && r.FromDate > after
            && (r.Status == LeaveStatus.Draft || r.Status == LeaveStatus.Submitted || r.Status == LeaveStatus.Approved))).ToListAsync(ct);

    public async Task<decimal> PendingDaysAsync(Guid employeeId, Guid leaveTypeId, Guid excludeRequestId, int year, CancellationToken ct)
        => (await Query(new Specification<LeaveRequest>(x => x.EmployeeId == employeeId && x.LeaveTypeId == leaveTypeId && x.Id != excludeRequestId && x.Status == LeaveStatus.Submitted && x.FromDate.Year == year))
            .Select(x => x.Days).ToListAsync(ct)).Sum();

    public async Task<Dictionary<(Guid EmployeeId, Guid LeaveTypeId), decimal>> PendingDaysByEmployeeAndTypeAsync(DateOnly from, DateOnly to, List<Guid> employeeIds, CancellationToken ct)
        => (await Query(new Specification<LeaveRequest>(r => r.Status == LeaveStatus.Submitted && r.FromDate >= from && r.FromDate <= to && employeeIds.Contains(r.EmployeeId))).ToListAsync(ct))
            .GroupBy(r => (r.EmployeeId, r.LeaveTypeId)).ToDictionary(g => g.Key, g => g.Sum(r => r.Days));

    public Task<PagedResult<LeaveRequestRow>> PageOfEmployeeAsync(PagedQuery paging, Guid employeeId, CancellationToken ct)
        => PageAsync(paging, LeaveRequestRow.Projection, new Specification<LeaveRequest>(r => r.EmployeeId == employeeId), ct);

    public Task<PagedResult<LeaveRequestRow>> PageOfEmployeesAsync(PagedQuery paging, List<Guid> employeeIds, LeaveStatus? status, CancellationToken ct)
        => PageAsync(paging, LeaveRequestRow.Projection, new Specification<LeaveRequest>(r => employeeIds.Contains(r.EmployeeId) && (status == null || r.Status == status)), ct);

    public Task<List<LeaveTypeDays>> ListApprovedDaysAsync(Guid? legalEntityId, DateOnly from, DateOnly to, CancellationToken ct)
        => Query(new Specification<LeaveRequest>(r => (legalEntityId == null || r.LegalEntityId == legalEntityId) && r.Status == LeaveStatus.Approved && r.FromDate >= from && r.FromDate <= to))
            .Select(r => new LeaveTypeDays(r.LeaveTypeId, r.Days)).ToListAsync(ct);
}
