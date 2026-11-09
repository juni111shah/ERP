using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class CreateMilestoneHandler(
    IProjectMilestoneRepository milestones,
    IProjectRepository projects,
    MilestoneRules rules,
    MilestoneDtoBuilder dtos,
    ProjectAccess access)
    : ICommandHandler<CreateMilestoneCommand, MilestoneDto>
{
    public async Task<MilestoneDto> Handle(CreateMilestoneCommand command, CancellationToken ct)
    {
        // 1. The project must be open; Invoiced is only ever set by billing; the fee caps fixed-fee milestones.
        var i = command.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        ProjectRules.EnsureOpen(p);
        await access.EnsureAsync(p, ct);
        if (i.Status == MilestoneStatus.Invoiced) throw new FieldValidationException("status", "A milestone becomes Invoiced only when its project is billed.");
        await rules.EnsureWithinFeeAsync(p, null, i, ct);

        // 2. Add it and save.
        var m = new ProjectMilestone
        {
            ProjectId = p.Id,
            Title = i.Title.Trim(),
            DueDate = i.DueDate,
            Amount = i.Amount,
            Status = i.Status,
            AchievedDate = MilestoneRules.AchievedDateFor(i),
        };
        milestones.Add(m);
        await milestones.SaveChangesAsync(ct);
        return await dtos.BuildAsync(m, ct);
    }
}
