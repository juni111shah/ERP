using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class DeleteMilestoneHandler(IProjectMilestoneRepository milestones, IProjectRepository projects, ProjectAccess access) : ICommandHandler<DeleteMilestoneCommand>
{
    public async Task<Unit> Handle(DeleteMilestoneCommand command, CancellationToken ct)
    {
        // 1. Load it; an invoiced milestone is final and the project must be open.
        var m = await milestones.GetForUpdateAsync(command.Id, ct);
        if (m.Status == MilestoneStatus.Invoiced) throw new ConflictException("An invoiced milestone cannot be deleted.");
        ProjectRules.EnsureOpen(await projects.GetAsync(m.ProjectId, ct));
        await access.EnsureProjectAsync(m.ProjectId, ct);

        // 2. Remove it.
        milestones.Remove(m);
        await milestones.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
