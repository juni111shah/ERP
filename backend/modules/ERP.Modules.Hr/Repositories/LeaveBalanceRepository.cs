using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class LeaveBalanceRepository(IRepository<LeaveBalance> inner) : RepositoryBase<LeaveBalance>(inner), ILeaveBalanceRepository
{
    public Task<LeaveBalance?> FindForYearAsync(Guid employeeId, Guid leaveTypeId, int year, CancellationToken ct)
        => Query(new Specification<LeaveBalance>(x => x.EmployeeId == employeeId && x.LeaveTypeId == leaveTypeId && x.Year == year)).FirstOrDefaultAsync(ct);

    public Task<LeaveBalance?> FindForYearForUpdateAsync(Guid employeeId, Guid leaveTypeId, int year, CancellationToken ct)
        => Tracked(new Specification<LeaveBalance>(x => x.EmployeeId == employeeId && x.LeaveTypeId == leaveTypeId && x.Year == year)).FirstOrDefaultAsync(ct);

    public async Task<Dictionary<(Guid EmployeeId, Guid LeaveTypeId), LeaveBalance>> MapForYearAsync(int year, List<Guid> employeeIds, CancellationToken ct)
        => (await Query(new Specification<LeaveBalance>(b => b.Year == year && employeeIds.Contains(b.EmployeeId))).ToListAsync(ct)).ToDictionary(b => (b.EmployeeId, b.LeaveTypeId));
}
