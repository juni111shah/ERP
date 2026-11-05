using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

public sealed record PostCostsResult(Guid ProjectId, int Entries, decimal Amount, Guid? JournalId, string? JournalNumber);

/// <summary>
/// Moves the cost of the project's approved time (cost rate x hours) and approved expenses into work in progress: Dr Work in progress, Cr Project cost
/// recovery. Every entry is posted once; running it again only picks up what is new.
/// </summary>
public sealed record PostProjectCosts(Guid ProjectId) : ICommand<PostCostsResult>, IRequirePermission { public string RequiredPermission => P.BillingManage; }

internal sealed class ProjectLedgerHandlers(
    IReadRepository<Project> projects,
    IRepository<ProjectTimesheet> timesheets,
    IRepository<ProjectExpense> expenses,
    IPostingService posting,
    IUnitOfWork uow) : IRequestHandler<PostProjectCosts, PostCostsResult>
{
    public Task<PostCostsResult> Handle(PostProjectCosts cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var p = await projects.GetAsync(cmd.ProjectId, token);
        if (p.IsTemplate) throw new ConflictException("A template has no costs.");
        var time = await timesheets.Tracked(new Specification<ProjectTimesheet>(t => t.ProjectId == p.Id && t.CostJournalId == null && t.CostAmount > 0 && t.ApprovalStatus == TimesheetStatus.Approved)).ToListAsync(token);
        var costs = await expenses.Tracked(new Specification<ProjectExpense>(e => e.ProjectId == p.Id && e.CostJournalId == null && e.Amount > 0 && e.Status == ExpenseStatus.Approved)).ToListAsync(token);
        var amount = ProjectMath.Round(time.Sum(t => t.CostAmount) + costs.Sum(e => e.Amount));
        if (time.Count + costs.Count == 0 || amount <= 0) return new PostCostsResult(p.Id, 0, 0m, null, null);

        var wip = await posting.AccountForRoleAsync(SystemRoles.Wip, token);
        var recovery = await posting.AccountForRoleAsync("projects.costrecovery", token);
        var memo = $"Project {p.Number} cost to work in progress";
        var journal = await posting.PostAsync(new PostingRequest(p.LegalEntityId, DateOnly.FromDateTime(DateTime.UtcNow), "projects.cost", p.Id, memo, p.Number,
            [new PostingLine(wip, amount, 0, memo), new PostingLine(recovery, 0, amount, memo)]), token);
        foreach (var t in time) t.CostJournalId = journal.Id;
        foreach (var e in costs) e.CostJournalId = journal.Id;
        await timesheets.SaveChangesAsync(token);
        await expenses.SaveChangesAsync(token);
        return new PostCostsResult(p.Id, time.Count + costs.Count, amount, journal.Id, journal.Number);
    }, ct);
}
