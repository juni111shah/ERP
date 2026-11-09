using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetProjectActivityHandler(
    IAuditEventRepository audit,
    IProjectRepository projects,
    IProjectTaskRepository tasks,
    IProjectMilestoneRepository milestones,
    IProjectExpenseRepository expenses,
    IProjectChangeOrderRepository changeOrders,
    IProjectRiskRepository risks,
    IProjectMemberRepository members,
    IProjectAcceptanceRepository acceptances,
    IProjectTimesheetRepository timesheets,
    ProjectPeople people,
    ProjectAccess access)
    : IQueryHandler<GetProjectActivityQuery, IReadOnlyList<ActivityDto>>
{
    /// <summary>Bookkeeping columns say nothing to a reader of the timeline.</summary>
    private static readonly HashSet<string> Noise = ["UpdatedAt", "UpdatedBy", "CreatedAt", "CreatedBy", "RowVersion", "OrgId", "IsDeleted", "DeletedAt", "DeletedBy", "ActualHours"];

    public async Task<IReadOnlyList<ActivityDto>> Handle(GetProjectActivityQuery query, CancellationToken ct)
    {
        // 1. Load the project.
        var p = await projects.GetAsync(query.ProjectId, ct);
        await access.EnsureAsync(p, ct);
        var id = p.Id;

        // 2. Every record of the project, with a kind and a title for the timeline.
        var titles = new Dictionary<string, (string Kind, string Title)> { [id.ToString()] = ("project", p.Name) };
        void Add(string kind, Guid key, string title) => titles[key.ToString()] = (kind, title);
        foreach (var t in await tasks.ListOfProjectAsync(id, ct)) Add("task", t.Id, $"{t.Number} {t.Title}");
        foreach (var m in await milestones.ListOfProjectAsync(id, ct)) Add("milestone", m.Id, m.Title);
        foreach (var e in await expenses.ListOfProjectAsync(id, ct)) Add("expense", e.Id, e.Description);
        foreach (var c in await changeOrders.ListForProjectAsync(id, ct)) Add("change order", c.Id, $"{c.Number} {c.Title}");
        foreach (var r in await risks.ListOfProjectAsync(id, ct)) Add("risk", r.Id, r.Title);
        foreach (var m in await members.ListForProjectAsync(id, ct)) Add("team", m.Id, m.Name);
        foreach (var a in await acceptances.ListOfProjectAsync(id, ct)) Add("sign-off", a.Id, a.Title);
        foreach (var t in await timesheets.ListOfProjectAsync(id, ct)) Add("time", t.Id, $"{t.WorkerName}, {t.Hours:0.##} h on {t.Date:yyyy-MM-dd}");

        // 3. Their latest audit events, leaving out updates that only touched bookkeeping columns.
        var keys = titles.Keys.ToList();
        var take = Math.Clamp(query.Take, 1, 200);
        var rows = await audit.ListLatestForRecordsAsync(keys, take, ct);
        var names = await people.NamesAsync(rows.Where(r => r.UserId != null).Select(r => r.UserId!.Value), ct);
        return rows.Select(r =>
        {
            var (kind, title) = titles[r.RecordId];
            var fields = (r.ChangedFields ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Where(f => !Noise.Contains(f)).ToList();
            return new { r, kind, title, fields };
        })
        .Where(x => x.r.Action != "Updated" || x.fields.Count > 0)
        .Select(x => new ActivityDto(x.r.OccurredAt, x.r.UserId is { } u ? names.GetValueOrDefault(u) : null, x.r.Action, x.kind, x.title, x.fields)).ToList();
    }
}
