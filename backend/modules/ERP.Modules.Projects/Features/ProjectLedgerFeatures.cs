using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modules.Projects.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

/// <summary>One journal of a cost posting: where the month's cost went.</summary>
public sealed record PostedCostJournalDto(Guid JournalId, string JournalNumber, DateOnly Date, decimal ToWorkInProgress, decimal ToExpense);

/// <summary>JournalId and JournalNumber name the first journal, for callers that expect one; Journals lists them all.</summary>
public sealed record PostCostsResult(Guid ProjectId, int Entries, decimal Amount, Guid? JournalId, string? JournalNumber,
    IReadOnlyList<PostedCostJournalDto>? Journals = null, decimal ToWorkInProgress = 0m, decimal ToExpense = 0m);

/// <summary>
/// Moves the cost of the project's approved time (cost rate x hours) and approved expenses out of project cost recovery, one journal per calendar month of the work, dated the
/// last day worked in that month. Billable work goes to work in progress until it is billed or the project closes; non-billable work (and anything on a project that has already
/// closed, which could never leave work in progress) goes straight to expense. Every entry is posted once; running it again only picks up what is new.
/// </summary>
public sealed record PostProjectCosts(Guid ProjectId) : ICommand<PostCostsResult>, IRequirePermission { public string RequiredPermission => P.BillingManage; }

/// <summary>Grouping and dating of cost journals; no I/O.</summary>
public static class ProjectCostPeriods
{
    /// <summary>The date of a month's journal: the last day worked, but never in the future and never in a locked period (the books would refuse it).</summary>
    public static DateOnly JournalDate(DateOnly lastWorkDate, DateOnly today, DateOnly? booksLockedTo)
    {
        var date = lastWorkDate > today ? today : lastWorkDate;
        return booksLockedTo is { } locked && date <= locked ? locked.AddDays(1) : date;
    }
}

internal sealed class ProjectLedgerHandlers(
    IReadRepository<Project> projects,
    IRepository<ProjectTimesheet> timesheets,
    IRepository<ProjectExpense> expenses,
    IReadRepository<Account> accounts,
    IPostingService posting,
    ProjectAccess access,
    ILegalEntityGuard guard,
    IUnitOfWork uow) : IRequestHandler<PostProjectCosts, PostCostsResult>
{
    /// <summary>Give an expense account this role to keep non-billable project cost apart from cost of sales.</summary>
    public const string NonBillableRole = "projects.nonbillable";

    private sealed record CostRow(DateOnly Date, decimal Cost, bool Expensed, Action<Guid> MarkPosted);

    public Task<PostCostsResult> Handle(PostProjectCosts cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var p = await projects.GetAsync(cmd.ProjectId, token);
        await access.EnsureAsync(p, token);
        if (p.IsTemplate) throw new ConflictException("A template has no costs.");
        // written-off work is not moved in: it has nothing left to be billed against
        var time = await timesheets.Tracked(new Specification<ProjectTimesheet>(t => t.ProjectId == p.Id && t.CostJournalId == null && t.CostAmount > 0 && t.ApprovalStatus == TimesheetStatus.Approved && !t.WrittenOff)).ToListAsync(token);
        var costs = await expenses.Tracked(new Specification<ProjectExpense>(e => e.ProjectId == p.Id && e.CostJournalId == null && e.Amount > 0 && e.Status == ExpenseStatus.Approved && !e.WrittenOff)).ToListAsync(token);
        var amount = ProjectMath.Round(time.Sum(t => t.CostAmount) + costs.Sum(e => e.Amount));
        if (time.Count + costs.Count == 0 || amount <= 0) return new PostCostsResult(p.Id, 0, 0m, null, null, []);

        var closed = p.Status is ProjectStatus.Completed or ProjectStatus.Cancelled;
        bool Expensed(bool billable) => closed || !billable || p.BillingType == ProjectBillingType.NonBillable;
        // an expensed entry has also left work in progress, so the same journal marks both steps and later releases skip it
        var rows = time.Select(t => new CostRow(t.Date, t.CostAmount, Expensed(t.IsBillable), j => { t.CostJournalId = j; if (Expensed(t.IsBillable)) t.WipReleaseJournalId = j; }))
            .Concat(costs.Select(e => new CostRow(e.Date, e.Amount, Expensed(e.IsBillable), j => { e.CostJournalId = j; if (Expensed(e.IsBillable)) e.WipReleaseJournalId = j; })))
            .ToList();

        var entity = await guard.RequireAsync(p.LegalEntityId, token);
        var today = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        var recovery = await posting.AccountForRoleAsync("projects.costrecovery", token);
        var journals = new List<PostedCostJournalDto>();
        foreach (var month in rows.GroupBy(r => new { r.Date.Year, r.Date.Month }).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month))
        {
            var toWip = ProjectMath.Round(month.Where(r => !r.Expensed).Sum(r => r.Cost));
            var toExpense = ProjectMath.Round(month.Where(r => r.Expensed).Sum(r => r.Cost));
            if (toWip + toExpense <= 0) continue;
            var memo = $"Project {p.Number} cost {month.Key.Year}-{month.Key.Month:00}";
            var lines = new List<PostingLine>();
            if (toWip > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync(SystemRoles.Wip, token), toWip, 0, $"{memo} to work in progress"));
            if (toExpense > 0) lines.Add(new PostingLine(await ExpenseAccountAsync(closed, token), toExpense, 0, $"{memo} to expense"));
            lines.Add(new PostingLine(recovery, 0, toWip + toExpense, memo));
            var date = ProjectCostPeriods.JournalDate(month.Max(r => r.Date), today, entity.BooksLockedTo);
            var journal = await posting.PostAsync(new PostingRequest(p.LegalEntityId, date, "projects.cost", p.Id, memo, p.Number, lines), token);
            foreach (var r in month) r.MarkPosted(journal.Id);
            journals.Add(new PostedCostJournalDto(journal.Id, journal.Number, date, toWip, toExpense));
        }
        await timesheets.SaveChangesAsync(token);
        await expenses.SaveChangesAsync(token);
        var first = journals.FirstOrDefault();
        return new PostCostsResult(p.Id, rows.Count, amount, first?.JournalId, first?.JournalNumber, journals, journals.Sum(j => j.ToWorkInProgress), journals.Sum(j => j.ToExpense));
    }, ct);

    /// <summary>Non-billable cost lands on the account with the non-billable role when the workspace has one, otherwise on cost of sales; so does anything posted after the project closed.</summary>
    private async Task<Guid> ExpenseAccountAsync(bool closed, CancellationToken ct)
    {
        if (closed) return await posting.AccountForRoleAsync("cogs", ct);
        var own = await accounts.Query(new Specification<Account>(a => a.SystemRole == NonBillableRole && a.IsActive)).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
        return own ?? await posting.AccountForRoleAsync("cogs", ct);
    }
}
