using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

// ---- activity timeline ----------------------------------------------------------------------------------------------------------
public sealed record ActivityDto(DateTime At, string? Who, string Action, string Kind, string Title, IReadOnlyList<string> Fields);
/// <summary>What happened on a project, newest first, read from the audit trail.</summary>
public sealed record GetProjectActivity(Guid ProjectId, int Take = 100) : IQuery<IReadOnlyList<ActivityDto>>, IRequirePermission { public string RequiredPermission => P.ProjectsRead; }

// ---- how this workspace runs its projects ----------------------------------------------------------------------------------------
public sealed record ProjectsConfigDto(bool TimesheetApproval, bool ExpenseApproval, bool AllowSelfApproval, decimal MaxHoursPerDay, decimal WeeklyCapacityHours, IReadOnlyList<int> WorkDays, decimal DefaultHourlyRate);
public sealed record GetProjectsConfig : IQuery<ProjectsConfigDto>;

// ---- budget by category -----------------------------------------------------------------------------------------------------------
public sealed record BudgetLineInput(Guid ProjectId, string Category, decimal Amount, decimal Hours = 0m);
/// <summary>Id is null for spending in a category that has no budget line.</summary>
public sealed record BudgetLineDto(Guid? Id, string Category, decimal Amount, decimal Hours, decimal Actual, decimal ActualHours);
public sealed record ListBudgetLines(Guid ProjectId) : IQuery<IReadOnlyList<BudgetLineDto>>, IRequirePermission { public string RequiredPermission => P.ProjectsRead; }
public sealed record SaveBudgetLine(Guid? Id, BudgetLineInput Input) : ICommand<BudgetLineDto>, IRequirePermission { public string RequiredPermission => P.ProjectsManage; }
public sealed record DeleteBudgetLine(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.ProjectsManage; }

public sealed class SaveBudgetLineValidator : AbstractValidator<SaveBudgetLine>
{
    public SaveBudgetLineValidator()
    {
        RuleFor(x => x.Input.ProjectId).NotEmpty();
        RuleFor(x => x.Input.Category).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Input.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.Hours).GreaterThanOrEqualTo(0);
    }
}

internal sealed class ProjectInsightHandlers(
    IReadRepository<AuditEvent> audit,
    IReadRepository<Project> projects,
    IReadRepository<ProjectTask> tasks,
    IReadRepository<ProjectMilestone> milestones,
    IReadRepository<ProjectExpense> expenses,
    IReadRepository<ProjectChangeOrder> changeOrders,
    IReadRepository<ProjectRisk> risks,
    IReadRepository<ProjectMember> members,
    IReadRepository<ProjectAcceptance> acceptances,
    IReadRepository<ProjectTimesheet> timesheets,
    IRepository<ProjectBudgetLine> budgetLines,
    ProjectPeople people,
    ProjectAccess access,
    ISettingsService settings) :
    IRequestHandler<GetProjectActivity, IReadOnlyList<ActivityDto>>,
    IRequestHandler<GetProjectsConfig, ProjectsConfigDto>,
    IRequestHandler<ListBudgetLines, IReadOnlyList<BudgetLineDto>>,
    IRequestHandler<SaveBudgetLine, BudgetLineDto>,
    IRequestHandler<DeleteBudgetLine, Unit>
{
    public const string LabourCategory = "Labour";

    /// <summary>Bookkeeping columns say nothing to a reader of the timeline.</summary>
    private static readonly HashSet<string> Noise = ["UpdatedAt", "UpdatedBy", "CreatedAt", "CreatedBy", "RowVersion", "OrgId", "IsDeleted", "DeletedAt", "DeletedBy", "ActualHours"];

    public async Task<IReadOnlyList<ActivityDto>> Handle(GetProjectActivity q, CancellationToken ct)
    {
        var p = await projects.GetAsync(q.ProjectId, ct);
        await access.EnsureAsync(p, ct);
        var id = p.Id;
        var titles = new Dictionary<string, (string Kind, string Title)> { [id.ToString()] = ("project", p.Name) };
        void Add(string kind, Guid key, string title) => titles[key.ToString()] = (kind, title);
        foreach (var t in await tasks.Query(new Specification<ProjectTask>(x => x.ProjectId == id)).ToListAsync(ct)) Add("task", t.Id, $"{t.Number} {t.Title}");
        foreach (var m in await milestones.Query(new Specification<ProjectMilestone>(x => x.ProjectId == id)).ToListAsync(ct)) Add("milestone", m.Id, m.Title);
        foreach (var e in await expenses.Query(new Specification<ProjectExpense>(x => x.ProjectId == id)).ToListAsync(ct)) Add("expense", e.Id, e.Description);
        foreach (var c in await changeOrders.Query(new Specification<ProjectChangeOrder>(x => x.ProjectId == id)).ToListAsync(ct)) Add("change order", c.Id, $"{c.Number} {c.Title}");
        foreach (var r in await risks.Query(new Specification<ProjectRisk>(x => x.ProjectId == id)).ToListAsync(ct)) Add("risk", r.Id, r.Title);
        foreach (var m in await members.Query(new Specification<ProjectMember>(x => x.ProjectId == id)).ToListAsync(ct)) Add("team", m.Id, m.Name);
        foreach (var a in await acceptances.Query(new Specification<ProjectAcceptance>(x => x.ProjectId == id)).ToListAsync(ct)) Add("sign-off", a.Id, a.Title);
        foreach (var t in await timesheets.Query(new Specification<ProjectTimesheet>(x => x.ProjectId == id)).ToListAsync(ct)) Add("time", t.Id, $"{t.WorkerName}, {t.Hours:0.##} h on {t.Date:yyyy-MM-dd}");
        var keys = titles.Keys.ToList();
        var take = Math.Clamp(q.Take, 1, 200);
        var rows = await audit.Query(new Specification<AuditEvent>(a => keys.Contains(a.RecordId))).OrderByDescending(a => a.OccurredAt).Take(take).ToListAsync(ct);
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

    public async Task<ProjectsConfigDto> Handle(GetProjectsConfig q, CancellationToken ct)
    {
        var week = ReportMath.ParseWorkWeek(await settings.GetAsync<string?>("projects.workWeekDays", null, ct));
        return new ProjectsConfigDto(
            await settings.GetAsync<bool?>("projects.timesheetApproval", null, ct) == true,
            await settings.GetAsync<bool?>("projects.expenseApproval", null, ct) == true,
            await settings.GetAsync<bool?>("projects.allowSelfApproval", null, ct) == true,
            await settings.GetAsync<decimal?>("projects.maxHoursPerDay", null, ct) ?? 16m,
            await settings.GetAsync<decimal?>("projects.weeklyCapacityHours", null, ct) ?? 40m,
            week.Select(d => (int)d).OrderBy(d => d).ToList(),
            await settings.GetAsync<decimal?>("projects.defaultHourlyRate", null, ct) ?? 0m);
    }

    public async Task<IReadOnlyList<BudgetLineDto>> Handle(ListBudgetLines q, CancellationToken ct)
    {
        await access.EnsureProjectAsync(q.ProjectId, ct);
        var id = q.ProjectId;
        var lines = await budgetLines.Query(new Specification<ProjectBudgetLine>(x => x.ProjectId == id)).ToListAsync(ct);
        var time = await timesheets.Query(new Specification<ProjectTimesheet>(x => x.ProjectId == id && x.ApprovalStatus != TimesheetStatus.Rejected)).ToListAsync(ct);
        var costs = await expenses.Query(new Specification<ProjectExpense>(x => x.ProjectId == id && x.Status == ExpenseStatus.Approved)).ToListAsync(ct);
        return BudgetLines.Build(lines, time, costs);
    }

    public async Task<BudgetLineDto> Handle(SaveBudgetLine cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        await access.EnsureProjectAsync(i.ProjectId, ct);
        var category = i.Category.Trim();
        var pid = i.ProjectId;
        var existing = await budgetLines.Query(new Specification<ProjectBudgetLine>(x => x.ProjectId == pid)).ToListAsync(ct);
        if (existing.Any(x => x.Id != cmd.Id && string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase))) throw new ConflictException($"There is already a budget line for {category}.");
        ProjectBudgetLine line;
        if (cmd.Id is { } id) line = await budgetLines.GetForUpdateAsync(id, ct); else { line = new ProjectBudgetLine { ProjectId = pid }; budgetLines.Add(line); }
        line.Category = category; line.Amount = i.Amount; line.Hours = i.Hours;
        await budgetLines.SaveChangesAsync(ct);
        return new BudgetLineDto(line.Id, line.Category, line.Amount, line.Hours, 0m, 0m);
    }

    public async Task<Unit> Handle(DeleteBudgetLine cmd, CancellationToken ct)
    {
        var line = await budgetLines.GetForUpdateAsync(cmd.Id, ct);
        await access.EnsureProjectAsync(line.ProjectId, ct);
        budgetLines.Remove(line);
        await budgetLines.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Budget lines against what was really spent; no I/O. Hours count as "Labour" and approved expenses under their own category.</summary>
public static class BudgetLines
{
    public static IReadOnlyList<BudgetLineDto> Build(IReadOnlyCollection<ProjectBudgetLine> lines, IReadOnlyCollection<ProjectTimesheet> time, IReadOnlyCollection<ProjectExpense> costs)
    {
        var actual = costs.GroupBy(e => e.Category, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount), StringComparer.OrdinalIgnoreCase);
        var labour = ProjectInsightHandlers.LabourCategory;
        actual[labour] = actual.GetValueOrDefault(labour) + time.Sum(t => t.CostAmount);
        decimal Hours(string category) => string.Equals(category, labour, StringComparison.OrdinalIgnoreCase) ? time.Sum(t => t.Hours) : 0m;
        var rows = lines.Select(l => new BudgetLineDto(l.Id, l.Category, l.Amount, l.Hours, actual.GetValueOrDefault(l.Category), Hours(l.Category))).ToList();
        foreach (var (category, spent) in actual)
            if (spent != 0 && !lines.Any(l => string.Equals(l.Category, category, StringComparison.OrdinalIgnoreCase)))
                rows.Add(new BudgetLineDto(null, category, 0m, 0m, spent, Hours(category)));
        return rows.OrderBy(r => r.Category == labour ? 0 : 1).ThenBy(r => r.Category).ToList();
    }
}
