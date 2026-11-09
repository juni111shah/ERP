using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

/// <summary>Turns billable work into invoice lines; no I/O.</summary>
public static class BillingLines
{
    public sealed record Line(string Description, decimal Quantity, decimal Price);

    public static decimal Charge(ProjectExpense e) => ProjectMath.Round(e.Amount * (1m + e.MarkupPercent / 100m));

    public static List<Line> Build(Project p, IReadOnlyList<ProjectTimesheet> time, IReadOnlyList<ProjectMilestone> milestones, IReadOnlyList<ProjectExpense> expenses,
        IReadOnlyDictionary<Guid, string> taskTitles, string grouping, decimal? progressAmount, decimal? progressPercent)
    {
        var lines = new List<Line>();
        switch (grouping)
        {
            case "person":
                foreach (var g in time.GroupBy(t => t.WorkerName).OrderBy(g => g.Key))
                    lines.Add(new($"{p.Number}: {g.Key} ({g.Sum(t => t.Hours):0.##} hrs)", 1, g.Sum(t => t.TotalAmount)));
                break;
            case "task":
                foreach (var g in time.GroupBy(t => t.TaskId).OrderBy(g => g.Key.HasValue ? taskTitles.GetValueOrDefault(g.Key.Value) : ""))
                    lines.Add(new($"{p.Number}: {(g.Key is { } id && taskTitles.TryGetValue(id, out var title) ? title : "General work")} ({g.Sum(t => t.Hours):0.##} hrs)", 1, g.Sum(t => t.TotalAmount)));
                break;
            case "project":
                if (time.Count > 0) lines.Add(new($"{p.Number}: {p.Name}, professional time ({time.Sum(t => t.Hours):0.##} hrs)", 1, time.Sum(t => t.TotalAmount)));
                break;
            default:
                foreach (var ts in time)
                    lines.Add(new($"{p.Number}: {ts.Description ?? "Timesheet"} ({ts.Hours:0.##} hrs @ {ts.HourlyRate:0.##})", ts.Hours, ts.HourlyRate));
                break;
        }
        foreach (var e in expenses) lines.Add(new($"{p.Number}: {e.Category}, {e.Description}", 1, Charge(e)));
        foreach (var m in milestones) lines.Add(new($"{p.Number}: {m.Title} (Milestone)", 1, m.Amount));
        if (progressAmount is { } pa && pa > 0) lines.Add(new($"{p.Number}: progress billing {progressPercent:0.##}% of the fixed fee", 1, pa));
        return lines.Where(l => l.Price != 0 || l.Quantity != 0).ToList();
    }

    public static decimal Discount(decimal subtotal, decimal percent) => percent <= 0 ? 0m : ProjectMath.Round(subtotal * percent / 100m);
}
