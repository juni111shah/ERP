using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for the shift roster.</summary>
public interface IShiftRosterRepository : IRepository<ShiftRoster>
{
    /// <summary>An employee's roster entries covering <paramref name="date"/>.</summary>
    Task<List<ShiftRoster>> ListCoveringAsync(Guid employeeId, DateOnly date, CancellationToken ct);

    /// <summary>The roster entries overlapping <paramref name="from"/> to <paramref name="to"/>.</summary>
    Task<List<ShiftRoster>> ListOverlappingAsync(DateOnly from, DateOnly to, CancellationToken ct);
}
