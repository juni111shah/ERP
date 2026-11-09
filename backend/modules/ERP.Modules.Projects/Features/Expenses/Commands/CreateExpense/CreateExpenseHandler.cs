using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class CreateExpenseHandler(
    IProjectExpenseRepository expenses,
    IProjectRepository projects,
    ExpenseRules rules,
    ExpenseDtoBuilder dtos,
    ProjectAccess access,
    ISettingsService settings)
    : ICommandHandler<CreateExpenseCommand, ExpenseDto>
{
    public async Task<ExpenseDto> Handle(CreateExpenseCommand command, CancellationToken ct)
    {
        // 1. The project must take costs.
        var i = command.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        ProjectRules.EnsureTrackable(p);
        await access.EnsureAsync(p, ct);

        // 2. A draft when expenses need approval, otherwise approved straight away.
        var approval = await settings.GetAsync<bool?>("projects.expenseApproval", p.LegalEntityId, ct) == true;
        var e = new ProjectExpense { ProjectId = p.Id, Status = approval ? ExpenseStatus.Draft : ExpenseStatus.Approved };

        // 3. Fill it in and save.
        await rules.FillAsync(e, p, i, ct);
        expenses.Add(e);
        await expenses.SaveChangesAsync(ct);
        return await dtos.BuildAsync(e, ct);
    }
}
