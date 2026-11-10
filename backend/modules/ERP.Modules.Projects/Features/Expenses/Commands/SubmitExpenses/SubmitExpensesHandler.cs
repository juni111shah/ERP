using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class SubmitExpensesHandler(IProjectExpenseRepository expenses, ExpenseRules rules, ProjectAccess access) : ICommandHandler<SubmitExpensesCommand, int>
{
    public async Task<int> Handle(SubmitExpensesCommand command, CancellationToken ct)
    {
        // 1. Every expense must exist and be one the caller may change.
        var rows = await expenses.ListByIdsForUpdateAsync(command.Ids, ct);
        if (rows.Count != command.Ids.Distinct().Count()) throw new NotFoundException("One of the expenses was not found.");
        await access.EnsureProjectsAsync(rows.Select(e => e.ProjectId), ct);
        foreach (var e in rows) rules.EnsureMayChange(e);

        // 2. Submit the drafts and the rejected ones, and save.
        var n = 0;
        foreach (var e in rows.Where(e => e.Status is ExpenseStatus.Draft or ExpenseStatus.Rejected)) { e.Status = ExpenseStatus.Submitted; e.RejectReason = null; n++; }
        await expenses.SaveChangesAsync(ct);
        return n;
    }
}
