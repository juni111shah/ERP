using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for the monthly attendance records.</summary>
public interface IAttendanceRecordRepository : IRepository<AttendanceRecord>
{
    /// <summary>The month's records of the employees, by employee id.</summary>
    Task<Dictionary<Guid, AttendanceRecord>> MapForMonthAsync(int year, int month, List<Guid> employeeIds, CancellationToken ct);

    /// <summary>The month's records of the employees, tracked for changes, by employee id.</summary>
    Task<Dictionary<Guid, AttendanceRecord>> MapForMonthForUpdateAsync(int year, int month, List<Guid> employeeIds, CancellationToken ct);

    /// <summary>An employee's record of a month, tracked for changes; null when there is none.</summary>
    Task<AttendanceRecord?> FindForMonthForUpdateAsync(Guid employeeId, int year, int month, CancellationToken ct);

    /// <summary>The records with unpaid absence (of one entity, or all) whose month index (year x 12 + month) is from <paramref name="fromMonthIndex"/> to <paramref name="toMonthIndex"/>.</summary>
    Task<List<MonthAbsence>> ListUnpaidAbsenceAsync(Guid? legalEntityId, int fromMonthIndex, int toMonthIndex, CancellationToken ct);
}

/// <summary>The unpaid absence days of one monthly record.</summary>
public sealed record MonthAbsence(int Year, int Month, decimal UnpaidAbsentDays);
