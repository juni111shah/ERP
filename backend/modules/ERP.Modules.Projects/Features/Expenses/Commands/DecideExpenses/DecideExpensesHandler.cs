using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class DecideExpensesHandler(
    IProjectExpenseRepository expenses,
    ProjectAccess access,
    ISettingsService settings,
    ICurrentUser user)
    : ICommandHandler<DecideExpensesCommand, int>
{
    public async Task<int> Handle(DecideExpensesCommand command, CancellationToken ct)
    {
        // 1. Every expense must exist; nobody approves their own unless the workspace allows it.
        var rows = await expenses.ListByIdsForUpdateAsync(command.Ids, ct);
        if (rows.Count != command.Ids.Distinct().Count()) throw new NotFoundException("One of the expenses was not found.");
        await access.EnsureProjectsAsync(rows.Select(e => e.ProjectId), ct);
        if (command.Approve && user.UserId is { } me && rows.Any(e => e.WorkerUserId == me && e.Status == ExpenseStatus.Submitted)
            && await settings.GetAsync<bool?>("projects.allowSelfApproval", null, ct) != true)
            throw new ConflictException("You cannot approve your own expense. Ask another approver.");

        // 2. Decide the submitted ones and save.
        var n = 0;
        foreach (var e in rows.Where(e => e.Status == ExpenseStatus.Submitted))
        {
            e.Status = command.Approve ? ExpenseStatus.Approved : ExpenseStatus.Rejected;
            e.RejectReason = command.Approve ? null : command.Reason?.Trim();
            n++;
        }
        await expenses.SaveChangesAsync(ct);
        return n;
    }
}
