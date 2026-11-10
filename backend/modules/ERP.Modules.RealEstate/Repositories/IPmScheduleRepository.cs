using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for preventive maintenance schedules. Implemented in PmScheduleRepository.cs.</summary>
public interface IPmScheduleRepository : IRepository<PmSchedule>
{
    /// <summary>The active schedules, tracked for changes.</summary>
    Task<List<PmSchedule>> ListActiveForUpdateAsync(CancellationToken ct);
}
