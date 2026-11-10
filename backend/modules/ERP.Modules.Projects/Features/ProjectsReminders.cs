using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Imports;
using ERP.Application.Reminders;
using ERP.Modules.Projects.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

/// <summary>Reminders for the assignee of a task that is due soon or overdue, and for risks past their due date.</summary>
internal sealed class ProjectsReminders(IReadRepository<ProjectTask> tasks, IReadRepository<ProjectRisk> risks, IReadRepository<Project> projects, IUserDirectory directory) : IReminderSource
{
    public string? ModuleKey => ProjectsModule.Key;

    public async Task<IReadOnlyList<ReminderCandidate>> GetDueAsync(DateOnly today, CancellationToken ct)
    {
        var result = new List<ReminderCandidate>();
        var horizon = today.AddDays(2);
        var due = await tasks.Query(new Specification<ProjectTask>(t => t.AssignedUserId != null && t.DueDate != null && t.DueDate <= horizon && t.Status != ProjectTaskStatus.Done && t.Status != ProjectTaskStatus.Cancelled)).ToListAsync(ct);
        var live = (await projects.Query(new Specification<Project>(p => p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled && !p.IsArchived)).Select(p => p.Id).ToListAsync(ct)).ToHashSet();
        var profiles = new Dictionary<Guid, string?>();
        foreach (var t in due.Where(t => live.Contains(t.ProjectId)))
        {
            var uid = t.AssignedUserId!.Value;
            if (!profiles.TryGetValue(uid, out var email)) profiles[uid] = email = (await directory.GetProfileAsync(uid, ct))?.Email;
            var overdue = t.DueDate!.Value < today;
            result.Add(new($"task-due:{t.Id}:{t.DueDate:yyyyMMdd}", overdue ? $"Task {t.Number} is overdue" : $"Task {t.Number} is due {t.DueDate:yyyy-MM-dd}", t.Title, $"/projects/tasks", P.TasksRead, email, overdue ? "danger" : "warning"));
        }
        var open = await risks.Query(new Specification<ProjectRisk>(r => r.Status == RiskStatus.Open && r.DueDate != null && r.DueDate < today)).ToListAsync(ct);
        foreach (var r in open)
            result.Add(new($"risk-due:{r.Id}:{r.DueDate:yyyyMMdd}", $"{r.Kind} past its due date: {r.Title}", r.Notes, "/projects/risks", P.RisksRead, null, "warning"));
        return result;
    }
}

/// <summary>CSV import of tasks: project number, title, assignee email, dates, estimate, priority.</summary>
internal sealed class ProjectTaskImport(ISender sender, IReadRepository<Project> projects, IUserDirectory directory) : IImportDefinition
{
    public string Key => "projects.tasks";
    public string Label => "Project tasks";
    public string? ModuleKey => ProjectsModule.Key;
    public string Permission => P.TasksManage;
    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        new("project", true, "PRJ-0001", "Project number"), new("title", true, "Prepare site survey"), new("description", false, ""),
        new("assigneeEmail", false, "ana@company.com", "Must be a member of the workspace"), new("startDate", false, "2026-12-01", "yyyy-mm-dd"), new("dueDate", false, "2026-12-15", "yyyy-mm-dd"),
        new("estimatedHours", false, "16"), new("priority", false, "Medium", "Low, Medium, High, Urgent"),
    ];

    public async Task ImportRowAsync(ImportRow r, CancellationToken ct)
    {
        var number = r.Require("project");
        var project = await projects.Query(new Specification<Project>(p => p.Number == number)).FirstOrDefaultAsync(ct) ?? throw new ImportRowException($"Project '{number}' was not found.");
        Guid? userId = null;
        if (r.Get("assigneeEmail") is { } email)
            userId = (await directory.ListMembersAsync(ct)).FirstOrDefault(m => m.Email.Equals(email, StringComparison.OrdinalIgnoreCase))?.Id ?? throw new ImportRowException($"No workspace member has the email '{email}'.");
        await sender.Send(new CreateTaskCommand(new TaskInput(project.Id, null, r.Require("title"), r.Get("description"), ProjectTaskStatus.Todo, r.Enum<TaskPriority>("priority", TaskPriority.Medium), null, r.Decimal("estimatedHours") ?? 0m, 0m,
            Date(r, "dueDate"), userId, null, Date(r, "startDate"))), ct);
    }

    private static DateOnly? Date(ImportRow r, string column) => r.Get(column) is { } v ? (DateOnly.TryParse(v, out var d) ? d : throw new ImportRowException($"'{column}' must be a date (yyyy-mm-dd).")) : null;
}
