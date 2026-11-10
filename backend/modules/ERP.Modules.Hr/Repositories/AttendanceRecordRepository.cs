using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class AttendanceRecordRepository(IRepository<AttendanceRecord> inner) : RepositoryBase<AttendanceRecord>(inner), IAttendanceRecordRepository
{
    public async Task<Dictionary<Guid, AttendanceRecord>> MapForMonthAsync(int year, int month, List<Guid> employeeIds, CancellationToken ct)
        => (await Query(new Specification<AttendanceRecord>(r => r.Year == year && r.Month == month && employeeIds.Contains(r.EmployeeId))).ToListAsync(ct)).ToDictionary(r => r.EmployeeId);

    public async Task<Dictionary<Guid, AttendanceRecord>> MapForMonthForUpdateAsync(int year, int month, List<Guid> employeeIds, CancellationToken ct)
        => (await Tracked(new Specification<AttendanceRecord>(r => r.Year == year && r.Month == month && employeeIds.Contains(r.EmployeeId))).ToListAsync(ct)).ToDictionary(r => r.EmployeeId);

    public Task<AttendanceRecord?> FindForMonthForUpdateAsync(Guid employeeId, int year, int month, CancellationToken ct)
        => Tracked(new Specification<AttendanceRecord>(x => x.EmployeeId == employeeId && x.Year == year && x.Month == month)).FirstOrDefaultAsync(ct);

    public Task<List<MonthAbsence>> ListUnpaidAbsenceAsync(Guid? legalEntityId, int fromMonthIndex, int toMonthIndex, CancellationToken ct)
        => Query(new Specification<AttendanceRecord>(r => (legalEntityId == null || r.LegalEntityId == legalEntityId) && r.Year * 12 + r.Month >= fromMonthIndex && r.Year * 12 + r.Month <= toMonthIndex && r.UnpaidAbsentDays > 0))
            .Select(r => new MonthAbsence(r.Year, r.Month, r.UnpaidAbsentDays)).ToListAsync(ct);
}
