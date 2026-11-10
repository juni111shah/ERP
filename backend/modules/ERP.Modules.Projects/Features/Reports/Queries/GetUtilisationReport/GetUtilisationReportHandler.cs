using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetUtilisationReportHandler(
    IProjectTimesheetRepository timesheets,
    IProjectAllocationRepository allocations,
    ProjectAccess access,
    ISettingsService settings)
    : IQueryHandler<GetUtilisationReportQuery, IReadOnlyList<UtilisationRow>>
{
    public async Task<IReadOnlyList<UtilisationRow>> Handle(GetUtilisationReportQuery q, CancellationToken ct)
    {
        // 1. The capacity of one person in the period, from the working week.
        var capacityPerWeek = await settings.GetAsync<decimal?>("projects.weeklyCapacityHours", null, ct) ?? 40m;
        var week = ReportMath.ParseWorkWeek(await settings.GetAsync<string?>("projects.workWeekDays", null, ct));
        var days = ReportMath.WorkdaysBetween(q.From, q.To, week);
        var capacity = Math.Round(days * capacityPerWeek / week.Count, 2);

        // 2. The time and the allocations of the period, on projects the caller may see.
        var visible = await access.VisibleIdsAsync(ct);
        var time = (await timesheets.ListNotRejectedBetweenAsync(q.From, q.To, ct)).Where(t => visible == null || visible.Contains(t.ProjectId)).ToList();
        var plan = (await allocations.ListOverlappingAsync(q.From, q.To, ct)).Where(a => visible == null || visible.Contains(a.ProjectId)).ToList();

        // 3. One row per person (a sign-in account, or a typed name).
        var people = time.Select(t => (Key: t.WorkerUserId?.ToString() ?? "n:" + t.WorkerName.ToLowerInvariant(), Name: t.WorkerName, UserId: t.WorkerUserId))
            .Concat(plan.Select(a => (Key: a.UserId.ToString(), Name: a.UserName, UserId: (Guid?)a.UserId))).GroupBy(x => x.Key).Select(g => g.First()).OrderBy(x => x.Name).ToList();
        var rows = new List<UtilisationRow>();
        foreach (var person in people)
        {
            var mine = time.Where(t => (t.WorkerUserId?.ToString() ?? "n:" + t.WorkerName.ToLowerInvariant()) == person.Key).ToList();
            var logged = mine.Sum(t => t.Hours); var billable = mine.Where(t => t.IsBillable && !t.WrittenOff).Sum(t => t.Hours);
            var planned = plan.Where(a => a.UserId.ToString() == person.Key).Sum(a => ReportMath.AllocatedHours(q.From, q.To, a.FromDate, a.ToDate, a.HoursPerWeek));
            rows.Add(new UtilisationRow(person.Name, person.UserId, logged, billable, capacity, planned, ReportMath.Percent(logged, capacity), ReportMath.Percent(billable, logged)));
        }
        return rows;
    }
}
