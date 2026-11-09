using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Modules.Projects.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

/// <summary>Voiding a project invoice puts its work back: the time, expenses, milestones and fixed-fee percentage can be billed again, and the cost goes back into work in progress.</summary>
public sealed class ProjectInvoiceVoidedHandler(IRepository<ProjectTimesheet> timesheets, IRepository<ProjectExpense> expenses, IRepository<ProjectMilestone> milestones, IRepository<ProjectProgressBilling> progress, IRepository<JournalEntry> journals, IReadRepository<Project> projects, IPostingService posting)
    : INotificationHandler<InvoiceVoided>
{
    public const string SourceType = "projects.project";

    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        if (n.SourceType != SourceType) return;
        var invoiceId = n.InvoiceId;
        var time = await timesheets.Tracked(new Specification<ProjectTimesheet>(x => x.InvoiceId == invoiceId)).ToListAsync(ct);
        var costs = await expenses.Tracked(new Specification<ProjectExpense>(x => x.InvoiceId == invoiceId)).ToListAsync(ct);
        // a closed project has no way to release work in progress a second time, so its cost stays in cost of sales
        var project = n.SourceId is { } projectId ? await projects.FindAsync(projectId, ct) : null;
        var closed = project?.Status is ProjectStatus.Completed or ProjectStatus.Cancelled;
        var releases = closed ? [] : time.Where(t => t.WipReleaseJournalId != null).Select(t => t.WipReleaseJournalId!.Value).Concat(costs.Where(e => e.WipReleaseJournalId != null).Select(e => e.WipReleaseJournalId!.Value)).Distinct().ToList();
        if (releases.Count > 0)
            foreach (var journal in await journals.Tracked(new Specification<JournalEntry>(j => releases.Contains(j.Id)).Include(j => j.Lines)).ToListAsync(ct))
                if (journal.Status == JournalStatus.Posted) await posting.ReverseTodayAsync(journal, $"Project invoice voided, cost back to work in progress", ct);
        foreach (var t in time) { t.IsBilled = false; t.InvoiceId = null; if (!closed) t.WipReleaseJournalId = null; }
        foreach (var e in costs) { e.IsBilled = false; e.InvoiceId = null; if (!closed) e.WipReleaseJournalId = null; }
        foreach (var m in await milestones.Tracked(new Specification<ProjectMilestone>(x => x.InvoiceId == invoiceId)).ToListAsync(ct))
        {
            m.Status = MilestoneStatus.Achieved; m.InvoiceId = null; // a system step: only a void puts an invoiced milestone back
        }
        foreach (var r in await progress.Tracked(new Specification<ProjectProgressBilling>(x => x.InvoiceId == invoiceId)).ToListAsync(ct)) r.Released = true;
        await timesheets.SaveChangesAsync(ct);
        await expenses.SaveChangesAsync(ct);
        await milestones.SaveChangesAsync(ct);
        await progress.SaveChangesAsync(ct);
    }
}
