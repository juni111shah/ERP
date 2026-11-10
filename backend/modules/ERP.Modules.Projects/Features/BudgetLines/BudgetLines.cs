using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

/// <summary>Budget lines against what was really spent; no I/O. Hours count as "Labour" and approved expenses under their own category.</summary>
public static class BudgetLines
{
    public const string LabourCategory = "Labour";

    public static IReadOnlyList<BudgetLineDto> Build(IReadOnlyCollection<ProjectBudgetLine> lines, IReadOnlyCollection<ProjectTimesheet> time, IReadOnlyCollection<ProjectExpense> costs)
    {
        var actual = costs.GroupBy(e => e.Category, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount), StringComparer.OrdinalIgnoreCase);
        var labour = LabourCategory;
        actual[labour] = actual.GetValueOrDefault(labour) + time.Sum(t => t.CostAmount);
        decimal Hours(string category) => string.Equals(category, labour, StringComparison.OrdinalIgnoreCase) ? time.Sum(t => t.Hours) : 0m;
        var rows = lines.Select(l => new BudgetLineDto(l.Id, l.Category, l.Amount, l.Hours, actual.GetValueOrDefault(l.Category), Hours(l.Category))).ToList();
        foreach (var (category, spent) in actual)
            if (spent != 0 && !lines.Any(l => string.Equals(l.Category, category, StringComparison.OrdinalIgnoreCase)))
                rows.Add(new BudgetLineDto(null, category, 0m, 0m, spent, Hours(category)));
        return rows.OrderBy(r => r.Category == labour ? 0 : 1).ThenBy(r => r.Category).ToList();
    }
}
