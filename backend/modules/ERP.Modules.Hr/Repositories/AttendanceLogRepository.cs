using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class AttendanceLogRepository(IRepository<AttendanceLog> inner) : RepositoryBase<AttendanceLog>(inner), IAttendanceLogRepository
{
    public Task<List<AttendanceLog>> ListBetweenAsync(DateOnly from, DateOnly to, Guid? employeeId, Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<AttendanceLog>(l => l.Date >= from && l.Date <= to && (employeeId == null || l.EmployeeId == employeeId) && (legalEntityId == null || l.LegalEntityId == legalEntityId))).ToListAsync(ct);

    public Task<AttendanceLog?> FindForDayAsync(Guid employeeId, DateOnly date, CancellationToken ct)
        => Query(new Specification<AttendanceLog>(l => l.EmployeeId == employeeId && l.Date == date)).FirstOrDefaultAsync(ct);

    public Task<AttendanceLog?> FindForDayForUpdateAsync(Guid employeeId, DateOnly date, CancellationToken ct)
        => Tracked(new Specification<AttendanceLog>(l => l.EmployeeId == employeeId && l.Date == date)).FirstOrDefaultAsync(ct);
}
