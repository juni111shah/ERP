using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class CloneProjectHandler(
    IProjectRepository projects,
    IProjectTaskRepository tasks,
    IProjectMilestoneRepository milestones,
    IProjectTaskDependencyRepository dependencies,
    IProjectMemberRepository members,
    IProjectRateRepository rates,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ProjectAccess access,
    ISender sender)
    : ICommandHandler<CloneProjectCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(CloneProjectCommand command, CancellationToken ct)
    {
        // 1. Load the source; every date moves by the shift of the start date.
        var src = await projects.GetAsync(command.Id, ct);
        await access.EnsureAsync(src, ct);
        var i = command.Input;
        var entity = await guard.RequireAsync(src.LegalEntityId, ct);
        var newStart = i.StartDate ?? src.StartDate;
        var shift = src.StartDate is { } s0 && newStart is { } n0 ? n0.DayNumber - s0.DayNumber : 0;
        DateOnly? Move(DateOnly? d) => d is { } v ? DateOnly.FromDayNumber(v.DayNumber + shift) : null;

        // 2. The copy starts in planning, with a new number.
        var copy = new Project
        {
            LegalEntityId = entity.Id, Name = i.Name.Trim(), Description = src.Description, CustomerId = i.AsTemplate ? null : i.CustomerId ?? (src.IsTemplate ? null : src.CustomerId),
            ManagerUserId = src.ManagerUserId, ManagerName = src.ManagerName, Status = ProjectStatus.Planning, BillingType = src.BillingType, BudgetAmount = src.BudgetAmount, ContractValue = src.ContractValue, BudgetHours = src.BudgetHours,
            TaxCodeId = src.TaxCodeId, StartDate = newStart, EndDate = Move(src.EndDate), IsTemplate = i.AsTemplate,
        };
        if (copy.CustomerId is { } cid) copy.CustomerName = (await sender.Send(new LookupCustomerNameQuery(cid), ct));
        copy.Number = await numbers.NextAsync(Docs.Project, entity.Id, null, ct);
        projects.Add(copy);
        await projects.SaveChangesAsync(ct);

        // 3. Milestones (pending again) and tasks (to do again, parents before subtasks).
        var srcMilestones = await milestones.ListLiveOfProjectAsync(src.Id, ct);
        var msMap = new Dictionary<Guid, Guid>();
        foreach (var m in srcMilestones)
        {
            var nm = new ProjectMilestone { ProjectId = copy.Id, Title = m.Title, DueDate = Move(m.DueDate) ?? m.DueDate, Amount = m.Amount, Status = MilestoneStatus.Pending };
            milestones.Add(nm); msMap[m.Id] = nm.Id;
        }
        var srcTasks = await tasks.ListLiveOfProjectAsync(src.Id, ct);
        var taskMap = new Dictionary<Guid, Guid>();
        foreach (var t in srcTasks.OrderBy(t => t.ParentTaskId == null ? 0 : 1))
        {
            var nt = new ProjectTask
            {
                ProjectId = copy.Id, MilestoneId = t.MilestoneId is { } mid && msMap.TryGetValue(mid, out var nm) ? nm : null, Number = await numbers.NextAsync("task", entity.Id, null, ct),
                Title = t.Title, Description = t.Description, Status = ProjectTaskStatus.Todo, Priority = t.Priority, EstimatedHours = t.EstimatedHours, ActualHours = 0,
                AssignedTo = i.IncludeTeam ? t.AssignedTo : null, AssignedUserId = i.IncludeTeam ? t.AssignedUserId : null, StartDate = Move(t.StartDate), DueDate = Move(t.DueDate), Labels = t.Labels,
                ParentTaskId = t.ParentTaskId is { } pid && taskMap.TryGetValue(pid, out var np) ? np : null,
            };
            tasks.Add(nt); taskMap[t.Id] = nt.Id;
        }

        // 4. Dependencies between the copied tasks, the team when asked, and the project's own rates.
        var oldIds = srcTasks.Select(t => t.Id).ToList();
        foreach (var d in await dependencies.ListForTasksAsync(oldIds, ct))
            if (taskMap.TryGetValue(d.TaskId, out var a) && taskMap.TryGetValue(d.DependsOnTaskId, out var b)) dependencies.Add(new ProjectTaskDependency { TaskId = a, DependsOnTaskId = b });
        if (i.IncludeTeam)
            foreach (var m in await members.ListForProjectAsync(src.Id, ct))
                members.Add(new ProjectMember { ProjectId = copy.Id, UserId = m.UserId, Name = m.Name, Role = m.Role });
        foreach (var r in await rates.ListForProjectAsync(src.Id, ct))
            rates.Add(new ProjectRate { ProjectId = copy.Id, UserId = r.UserId, Role = r.Role, BillRate = r.BillRate, CostRate = r.CostRate });

        // 5. Save and answer with the new project.
        await projects.SaveChangesAsync(ct);
        return await sender.Send(new GetProjectQuery(copy.Id), ct);
    }
}
