using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Domain.Organizations;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

/// <summary>Honours the members-only setting like the Projects screen, and leaves out templates and archived projects like its default view.</summary>
internal sealed class ProjectsAssistantReader(IReadRepository<Project> projects, IReadRepository<LegalEntity> entities, ProjectAccess access) : IAssistantReader
{
    public string Key => "projects";
    public string Description => "Live projects, newest first: number, name, customer, manager, status, budget and dates.";
    public string? ModuleKey => ProjectsModule.Key;
    public string Permission => P.ProjectsRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = projects.Query().Where(p => !p.IsTemplate && !p.IsArchived);
        if (await access.VisibleIdsAsync(ct) is { } visible) query = query.Where(p => visible.Contains(p.Id));
        if (search is not null) query = query.Where(p => p.Number.Contains(search) || p.Name.Contains(search) || (p.CustomerName != null && p.CustomerName.Contains(search)));
        return await query.OrderByDescending(p => p.CreatedAt).Take(take).Select(p => new { p.Number, p.Name, Customer = p.CustomerName, Manager = p.ManagerName, Status = p.Status.ToString(), Currency = entities.Query(null).Where(e => e.Id == p.LegalEntityId).Select(e => e.BaseCurrency).FirstOrDefault(), p.BudgetAmount, p.StartDate, p.EndDate, Link = "/projects/" + p.Id }).ToListAsync(ct);
    }
}

/// <summary>A task has no legal entity of its own: going through its project keeps the legal-entity limit, the members-only setting and the live-projects rule.</summary>
internal sealed class ProjectTasksAssistantReader(IReadRepository<ProjectTask> tasks, IReadRepository<Project> projects, ProjectAccess access) : IAssistantReader
{
    public string Key => "project_tasks";
    public string Description => "Project tasks, newest first: number, title, project, status, priority, assignee and due date.";
    public string? ModuleKey => ProjectsModule.Key;
    public string Permission => P.TasksRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var live = projects.Query().Where(p => !p.IsTemplate && !p.IsArchived);
        if (await access.VisibleIdsAsync(ct) is { } visible) live = live.Where(p => visible.Contains(p.Id));
        var query = tasks.Query().Join(live, t => t.ProjectId, p => p.Id, (t, p) => new { Task = t, ProjectName = p.Name });
        if (search is not null) query = query.Where(x => x.Task.Number.Contains(search) || x.Task.Title.Contains(search) || x.ProjectName.Contains(search) || (x.Task.AssignedTo != null && x.Task.AssignedTo.Contains(search)));
        return await query.OrderByDescending(x => x.Task.CreatedAt).Take(take).Select(x => new { x.Task.Number, x.Task.Title, Project = x.ProjectName, Status = x.Task.Status.ToString(), Priority = x.Task.Priority.ToString(), x.Task.AssignedTo, x.Task.DueDate, Link = "/projects/" + x.Task.ProjectId + "?tab=tasks" }).ToListAsync(ct);
    }
}

internal static class ProjectsAssistantHelp
{
    public static readonly IReadOnlyList<AssistantHelpArticle> Articles =
    [
        new("Create a project", "Go to Projects > Projects, press New project, fill in the details and press Save. From a template starts a copy of a saved template.", "/projects/list", P.ProjectsRead),
        new("Find what is in a project", "In Projects > Projects open the project by its name. Its tabs show the overview, tasks, milestones, time and expenses, billing and budget, team, and risks and changes.", "/projects/list", P.ProjectsRead),
        new("Add a task to a project", "Go to Projects > Tasks and press New task, or use the same button on Projects > Task board.", "/projects/tasks", P.TasksRead),
        new("Log time on a project", "Go to Project time and money > Timesheets and press Log time. Week view opens a whole week to fill in; press Save week when you are done.", "/projects/timesheets", P.TimesheetsRead),
    ];
}
