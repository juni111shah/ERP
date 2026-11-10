using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class SaveTimesheetWeekHandler(
    IProjectTimesheetRepository timesheets,
    IProjectTaskRepository tasks,
    TimesheetEntries entries,
    TimesheetDtoBuilder dtos,
    IUnitOfWork uow)
    : ICommandHandler<SaveTimesheetWeekCommand, IReadOnlyList<TimesheetDto>>
{
    public Task<IReadOnlyList<TimesheetDto>> Handle(SaveTimesheetWeekCommand command, CancellationToken ct) => uow.InTransactionAsync<IReadOnlyList<TimesheetDto>>(async token =>
    {
        // 1. Each row of the grid: an id with 0 hours removes, an id changes the hours, no id with hours creates.
        var touched = new List<ProjectTimesheet>(); var taskIds = new HashSet<Guid>();
        foreach (var i in command.Items)
        {
            if (i.Id is { } id)
            {
                var existing = await timesheets.GetForUpdateAsync(id, token);
                if (existing.TaskId is { } t0) taskIds.Add(t0);
                if (i.Hours == 0) { await entries.DeleteAsync(existing, token); await timesheets.SaveChangesAsync(token); continue; }
                // the grid only changes hours: everything else on the entry stays as it was
                await entries.UpdateAsync(existing, i with { Description = i.Description ?? existing.Description, HourlyRate = i.HourlyRate ?? existing.HourlyRate, CostRate = i.CostRate ?? existing.CostRate, IsBillable = existing.IsBillable, WorkerUserId = existing.WorkerUserId, WorkerName = existing.WorkerName, TaskId = existing.TaskId }, token);
                touched.Add(existing);
            }
            else if (i.Hours > 0) touched.Add(await entries.CreateAsync(i, token));
            await timesheets.SaveChangesAsync(token);
        }

        // 2. The actual hours of every task touched follow the time logged.
        foreach (var t in touched) if (t.TaskId is { } tid) taskIds.Add(tid);
        foreach (var tid in taskIds) await TaskHours.RecalculateAsync(timesheets, tasks, tid, token);
        await timesheets.SaveChangesAsync(token);

        // 3. Answer with the entries that remain.
        var result = new List<TimesheetDto>();
        foreach (var t in touched) result.Add(await dtos.BuildAsync(t, token));
        return result;
    }, ct);
}
