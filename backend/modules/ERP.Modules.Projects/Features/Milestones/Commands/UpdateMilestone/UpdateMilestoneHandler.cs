using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class UpdateMilestoneHandler(
    IProjectMilestoneRepository milestones,
    IProjectRepository projects,
    MilestoneRules rules,
    MilestoneDtoBuilder dtos,
    ProjectAccess access)
    : ICommandHandler<UpdateMilestoneCommand, MilestoneDto>
{
    public async Task<MilestoneDto> Handle(UpdateMilestoneCommand command, CancellationToken ct)
    {
        // 1. Load it; an invoiced milestone is final and the project must be open.
        var m = await milestones.GetForUpdateAsync(command.Id, ct);
        var i = command.Input;
        if (m.Status == MilestoneStatus.Invoiced) throw new ConflictException("An invoiced milestone cannot be edited.");
        var project = await projects.GetAsync(m.ProjectId, ct);
        ProjectRules.EnsureOpen(project);
        await access.EnsureAsync(project, ct);

        // 2. The fee cap and the status move.
        await rules.EnsureWithinFeeAsync(project, m.Id, i, ct);
        if (i.Status != m.Status) ProjectsTransitions.Milestone.Ensure(m.Status, i.Status, isSystem: false);

        // 3. Apply the form and save.
        var achieved = MilestoneRules.AchievedDateFor(i);
        m.Title = i.Title.Trim();
        m.DueDate = i.DueDate;
        m.Amount = i.Amount;
        m.Status = i.Status;
        m.AchievedDate = achieved;

        await milestones.SaveChangesAsync(ct);
        return await dtos.BuildAsync(m, ct);
    }
}
