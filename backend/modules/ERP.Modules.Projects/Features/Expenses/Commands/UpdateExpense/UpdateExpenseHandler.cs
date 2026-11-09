using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class UpdateExpenseHandler(
    IProjectExpenseRepository expenses,
    IProjectRepository projects,
    ExpenseRules rules,
    ExpenseDtoBuilder dtos,
    ProjectAccess access,
    ISettingsService settings)
    : ICommandHandler<UpdateExpenseCommand, ExpenseDto>
{
    public async Task<ExpenseDto> Handle(UpdateExpenseCommand command, CancellationToken ct)
    {
        // 1. Load it; billed, written-off and posted expenses are final.
        var e = await expenses.GetForUpdateAsync(command.Id, ct);
        if (e.IsBilled) throw new ConflictException("A billed expense cannot be edited.");
        if (e.WrittenOff) throw new ConflictException("An expense that was written off cannot be edited.");
        if (e.CostJournalId != null) throw new ConflictException("This expense is already in the ledger, so it cannot be changed. Write it off or record a correcting expense.");

        // 2. The project must take costs, the caller may change it, and under approval it must not be on its way.
        var p = await projects.GetAsync(e.ProjectId, ct);
        ProjectRules.EnsureTrackable(p);
        await access.EnsureAsync(p, ct);
        rules.EnsureMayChange(e);
        if (e.Status is ExpenseStatus.Submitted or ExpenseStatus.Approved && await settings.GetAsync<bool?>("projects.expenseApproval", p.LegalEntityId, ct) == true)
            throw new ConflictException("A submitted or approved expense cannot be edited. Ask for it to be rejected first.");

        // 3. Apply the form (a rejected expense goes back to draft) and save.
        await rules.FillAsync(e, p, command.Input, ct);
        if (e.Status == ExpenseStatus.Rejected) { e.Status = ExpenseStatus.Draft; e.RejectReason = null; }
        await expenses.SaveChangesAsync(ct);
        return await dtos.BuildAsync(e, ct);
    }
}
