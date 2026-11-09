using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetBirthdaysAnniversariesHandler(IEmployeeRepository employees, IClock clock) : IQueryHandler<GetBirthdaysAnniversariesQuery, HrOccasionsDto>
{
    public async Task<HrOccasionsDto> Handle(GetBirthdaysAnniversariesQuery query, CancellationToken ct)
    {
        // 1. The employees not terminated.
        var q = query;
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var staff = await employees.ListOccasionRowsAsync(q.LegalEntityId, ct);

        // 2. The next birthday and anniversary of each that falls within the window.
        var birthdays = new List<HrOccasion>(); var anniversaries = new List<HrOccasion>();
        foreach (var e in staff.OrderBy(e => e.Number))
        {
            if (EmployeeReportFields.BirthDate(e.DateOfBirth, e.CustomFields) is { } born && Next(born, today) is var b && b.DayNumber - today.DayNumber <= q.Days)
                birthdays.Add(new HrOccasion(e.Id, e.Number, e.FullName, b, b.DayNumber - today.DayNumber, b.Year - born.Year));
            var a = Next(e.JoinDate, today);
            if (a.Year - e.JoinDate.Year >= 1 && a.DayNumber - today.DayNumber <= q.Days)
                anniversaries.Add(new HrOccasion(e.Id, e.Number, e.FullName, a, a.DayNumber - today.DayNumber, a.Year - e.JoinDate.Year));
        }
        return new HrOccasionsDto(today, q.Days, birthdays.OrderBy(o => o.Date).ThenBy(o => o.Number).ToList(), anniversaries.OrderBy(o => o.Date).ThenBy(o => o.Number).ToList());
    }

    /// <summary>The next occurrence of a yearly date on or after today (29 February falls on 28 February in other years).</summary>
    private static DateOnly Next(DateOnly original, DateOnly today)
    {
        DateOnly In(int year) => original.Month == 2 && original.Day == 29 && !DateTime.IsLeapYear(year) ? new DateOnly(year, 2, 28) : new DateOnly(year, original.Month, original.Day);
        var d = In(today.Year);
        if (d < today) d = In(today.Year + 1);
        return d;
    }
}
