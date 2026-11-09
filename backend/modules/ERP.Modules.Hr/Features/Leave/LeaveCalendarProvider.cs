using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

/// <summary>Builds the working-day calendar (weekend setting + public holidays) for a date range.</summary>
public sealed class LeaveCalendarProvider(IReadRepository<PublicHoliday> holidays, ISettingsService settings)
{
    public async Task<LeaveCalendar> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var days = (await holidays.Query(new Specification<PublicHoliday>(h => h.Date >= from && h.Date <= to)).Select(h => h.Date).ToListAsync(ct)).ToHashSet();
        return new LeaveCalendar(LeaveCalendar.ParseWeekend(await settings.GetAsync<string>("hr.weekendDays", null, ct)), days);
    }
}
