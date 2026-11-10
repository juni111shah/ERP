using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

/// <summary>Builds the milestone view with its project name. Shared by the milestone handlers.</summary>
public sealed class MilestoneDtoBuilder(IProjectRepository projects)
{
    public async Task<MilestoneDto> BuildAsync(ProjectMilestone m, CancellationToken ct)
    {
        var p = await projects.GetAsync(m.ProjectId, ct);
        return new MilestoneDto(
            m.Id,
            m.ProjectId,
            p.Name,
            m.Title,
            m.DueDate,
            m.Amount,
            m.Status,
            m.AchievedDate,
            m.CreatedAt);
    }
}
