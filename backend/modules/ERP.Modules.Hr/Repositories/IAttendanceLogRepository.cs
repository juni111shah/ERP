using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for the daily attendance logs.</summary>
public interface IAttendanceLogRepository : IRepository<AttendanceLog>
{
    /// <summary>The logs from <paramref name="from"/> to <paramref name="to"/>, of one employee and/or one legal entity when given.</summary>
    Task<List<AttendanceLog>> ListBetweenAsync(DateOnly from, DateOnly to, Guid? employeeId, Guid? legalEntityId, CancellationToken ct);

    /// <summary>An employee's log of one day, read-only; null when there is none.</summary>
    Task<AttendanceLog?> FindForDayAsync(Guid employeeId, DateOnly date, CancellationToken ct);

    /// <summary>An employee's log of one day, tracked for changes; null when there is none.</summary>
    Task<AttendanceLog?> FindForDayForUpdateAsync(Guid employeeId, DateOnly date, CancellationToken ct);
}
