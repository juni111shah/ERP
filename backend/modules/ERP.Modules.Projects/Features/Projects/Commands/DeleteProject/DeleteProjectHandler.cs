using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class DeleteProjectHandler(
    IProjectRepository projects,
    IProjectTimesheetRepository timesheets,
    IProjectExpenseRepository expenses,
    IProjectMilestoneRepository milestones,
    IProjectProgressBillingRepository progress,
    ProjectAccess access)
    : ICommandHandler<DeleteProjectCommand>
{
    public async Task<Unit> Handle(DeleteProjectCommand command, CancellationToken ct)
    {
        // 1. Load it.
        var p = await projects.GetForUpdateAsync(command.Id, ct);
        await access.EnsureAsync(p, ct);

        // 2. Nothing may be invoiced and no cost may be in the ledger.
        if (await timesheets.AnyAsync(x => x.ProjectId == p.Id && x.IsBilled, ct) || await expenses.AnyAsync(x => x.ProjectId == p.Id && x.IsBilled, ct)
            || await milestones.AnyAsync(x => x.ProjectId == p.Id && x.Status == MilestoneStatus.Invoiced, ct) || await progress.AnyAsync(x => x.ProjectId == p.Id && !x.Released, ct))
            throw new ConflictException("A project with invoiced work cannot be deleted. Complete or cancel it instead.");
        if (await timesheets.AnyAsync(x => x.ProjectId == p.Id && x.CostJournalId != null, ct) || await expenses.AnyAsync(x => x.ProjectId == p.Id && x.CostJournalId != null, ct))
            throw new ConflictException("The cost of this project is already in the ledger, so it cannot be deleted. Complete or cancel it instead.");

        // 3. Remove it.
        projects.Remove(p);
        await projects.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
