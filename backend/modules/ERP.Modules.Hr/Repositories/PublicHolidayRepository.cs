using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class PublicHolidayRepository(IRepository<PublicHoliday> inner) : RepositoryBase<PublicHoliday>(inner), IPublicHolidayRepository
{
    public async Task<HashSet<DateOnly>> DatesAsync(CancellationToken ct) => (await Query().Select(h => h.Date).ToListAsync(ct)).ToHashSet();
}
