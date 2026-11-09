using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class ArchiveProjectHandler(IProjectRepository projects, ProjectAccess access, ISender sender) : ICommandHandler<ArchiveProjectCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(ArchiveProjectCommand command, CancellationToken ct)
    {
        // 1. Load it; only a project that is not running can be archived.
        var p = await projects.GetForUpdateAsync(command.Id, ct);
        await access.EnsureAsync(p, ct);
        if (command.Archive && p.Status is ProjectStatus.Active or ProjectStatus.OnHold) throw new ConflictException("Complete or cancel the project before archiving it.");

        // 2. Flag, save and answer with the detail view.
        p.IsArchived = command.Archive;
        await projects.SaveChangesAsync(ct);
        return await sender.Send(new GetProjectQuery(p.Id), ct);
    }
}
