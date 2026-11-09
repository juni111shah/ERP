using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for public holidays.</summary>
public interface IPublicHolidayRepository : IRepository<PublicHoliday>
{
    /// <summary>The date of every public holiday.</summary>
    Task<HashSet<DateOnly>> DatesAsync(CancellationToken ct);
}
