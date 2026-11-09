using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class PostProjectCostsHandler(
    IProjectRepository projects,
    IProjectTimesheetRepository timesheets,
    IProjectExpenseRepository expenses,
    IAccountRepository accounts,
    IPostingService posting,
    ProjectAccess access,
    ILegalEntityGuard guard,
    IUnitOfWork uow)
    : ICommandHandler<PostProjectCostsCommand, PostCostsResult>
{
    /// <summary>Give an expense account this role to keep non-billable project cost apart from cost of sales.</summary>
    public const string NonBillableRole = "projects.nonbillable";

    private sealed record CostRow(DateOnly Date, decimal Cost, bool Expensed, Action<Guid> MarkPosted);

    public Task<PostCostsResult> Handle(PostProjectCostsCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Load the project and what is approved and not yet posted.
        var p = await projects.GetAsync(command.ProjectId, token);
        await access.EnsureAsync(p, token);
        if (p.IsTemplate) throw new ConflictException("A template has no costs.");
        // written-off work is not moved in: it has nothing left to be billed against
        var time = await timesheets.ListUnpostedCostForUpdateAsync(p.Id, token);
        var costs = await expenses.ListUnpostedCostForUpdateAsync(p.Id, token);
        var amount = ProjectMath.Round(time.Sum(t => t.CostAmount) + costs.Sum(e => e.Amount));
        if (time.Count + costs.Count == 0 || amount <= 0) return new PostCostsResult(p.Id, 0, 0m, null, null, []);

        // 2. Billable work goes to work in progress; the rest straight to expense.
        var closed = p.Status is ProjectStatus.Completed or ProjectStatus.Cancelled;
        bool Expensed(bool billable) => closed || !billable || p.BillingType == ProjectBillingType.NonBillable;
        // an expensed entry has also left work in progress, so the same journal marks both steps and later releases skip it
        var rows = time.Select(t => new CostRow(t.Date, t.CostAmount, Expensed(t.IsBillable), j => { t.CostJournalId = j; if (Expensed(t.IsBillable)) t.WipReleaseJournalId = j; }))
            .Concat(costs.Select(e => new CostRow(e.Date, e.Amount, Expensed(e.IsBillable), j => { e.CostJournalId = j; if (Expensed(e.IsBillable)) e.WipReleaseJournalId = j; })))
            .ToList();

        // 3. One journal per calendar month of the work.
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

        // 4. Save and answer with the journals.
        await timesheets.SaveChangesAsync(token);
        await expenses.SaveChangesAsync(token);
        var first = journals.FirstOrDefault();
        return new PostCostsResult(p.Id, rows.Count, amount, first?.JournalId, first?.JournalNumber, journals, journals.Sum(j => j.ToWorkInProgress), journals.Sum(j => j.ToExpense));
    }, ct);

    /// <summary>Non-billable cost lands on the account with the non-billable role when the workspace has one, otherwise on cost of sales; so does anything posted after the project closed.</summary>
    private async Task<Guid> ExpenseAccountAsync(bool closed, CancellationToken ct)
    {
        if (closed) return await posting.AccountForRoleAsync("cogs", ct);
        var own = await accounts.FindActiveIdWithRoleAsync(NonBillableRole, ct);
        return own ?? await posting.AccountForRoleAsync("cogs", ct);
    }
}
