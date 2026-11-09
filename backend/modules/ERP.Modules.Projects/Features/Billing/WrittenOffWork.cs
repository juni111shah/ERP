using ERP.Application.Finance;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

/// <summary>Written-off work earns nothing more: its cost that is still in work in progress goes to cost of sales. The caller saves.</summary>
internal static class WrittenOffWork
{
    public static async Task ReleaseAsync(IPostingService posting, Project p, IReadOnlyList<ProjectTimesheet> time, IReadOnlyList<ProjectExpense> costs, CancellationToken ct)
    {
        var postedTime = time.Where(t => t.CostJournalId != null && t.WipReleaseJournalId == null).ToList();
        var postedCosts = costs.Where(e => e.CostJournalId != null && e.WipReleaseJournalId == null).ToList();
        var journal = await ProjectWip.ReleaseAsync(posting, p, postedTime.Sum(t => t.CostAmount) + postedCosts.Sum(e => e.Amount), p.Number, $"Project {p.Number} work written off, work in progress to cost of sales", ct);
        if (journal is null) return;
        foreach (var t in postedTime) t.WipReleaseJournalId = journal.Id;
        foreach (var e in postedCosts) e.WipReleaseJournalId = journal.Id;
    }
}
