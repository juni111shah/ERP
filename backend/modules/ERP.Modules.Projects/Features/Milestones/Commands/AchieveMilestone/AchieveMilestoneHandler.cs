using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class AchieveMilestoneHandler(
    IProjectMilestoneRepository milestones,
    IProjectRepository projects,
    MilestoneDtoBuilder dtos,
    ProjectAccess access)
    : ICommandHandler<AchieveMilestoneCommand, MilestoneDto>
{
    public async Task<MilestoneDto> Handle(AchieveMilestoneCommand command, CancellationToken ct)
    {
        // 1. Load it; the project must be open and the move allowed.
        var m = await milestones.GetForUpdateAsync(command.Id, ct);
        ProjectRules.EnsureOpen(await projects.GetAsync(m.ProjectId, ct));
        await access.EnsureProjectAsync(m.ProjectId, ct);
        ProjectsTransitions.Milestone.Ensure(m.Status, MilestoneStatus.Achieved, isSystem: false);

        // 2. The achieved date is today unless given, and never in the future.
        var achieved = command.AchievedDate ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        if (achieved > DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow)) throw new FieldValidationException("achievedDate", "The achieved date cannot be in the future.");

        // 3. Mark it and save.
        m.Status = MilestoneStatus.Achieved;
        m.AchievedDate = achieved;
        await milestones.SaveChangesAsync(ct);
        return await dtos.BuildAsync(m, ct);
    }
}
