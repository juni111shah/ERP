using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class DeleteExpenseHandler(
    IProjectExpenseRepository expenses,
    IProjectRepository projects,
    ExpenseRules rules,
    ProjectAccess access,
    ISettingsService settings)
    : ICommandHandler<DeleteExpenseCommand>
{
    public async Task<Unit> Handle(DeleteExpenseCommand command, CancellationToken ct)
    {
        // 1. Load it; billed and posted expenses stay.
        var e = await expenses.GetForUpdateAsync(command.Id, ct);
        if (e.IsBilled) throw new ConflictException("A billed expense cannot be deleted.");
        if (e.CostJournalId != null) throw new ConflictException("This expense is already in the ledger, so it cannot be deleted. Write it off instead.");

        // 2. The project must take costs, the caller may change it, and it must not be on its way through approval.
        var p = await projects.GetAsync(e.ProjectId, ct);
        ProjectRules.EnsureTrackable(p);
        await access.EnsureAsync(p, ct);
        rules.EnsureMayChange(e);
        if (e.Status == ExpenseStatus.Submitted) throw new ConflictException("A submitted expense cannot be deleted. Ask for it to be rejected first.");
        if (e.Status == ExpenseStatus.Approved && await settings.GetAsync<bool?>("projects.expenseApproval", p.LegalEntityId, ct) == true)
            throw new ConflictException("An approved expense cannot be deleted. Ask for it to be rejected first.");

        // 3. Remove it.
        expenses.Remove(e);
        await expenses.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
