using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class AddMemberHandler(IProjectMemberRepository members, IProjectRepository projects, ProjectPeople people, ProjectAccess access) : ICommandHandler<AddMemberCommand, MemberDto>
{
    public async Task<MemberDto> Handle(AddMemberCommand command, CancellationToken ct)
    {
        // 1. The project must be open; the person must be a workspace member not yet on the team.
        var p = await projects.GetAsync(command.ProjectId, ct);
        ProjectRules.EnsureOpen(p);
        await access.EnsureAsync(p, ct);
        var name = await people.RequireNameAsync(command.UserId, "userId", ct);
        if (await members.AnyAsync(m => m.ProjectId == p.Id && m.UserId == command.UserId, ct)) throw new ConflictException($"{name} is already on the team.");

        // 2. Add them and save.
        var m = new ProjectMember { ProjectId = p.Id, UserId = command.UserId, Name = name, Role = string.IsNullOrWhiteSpace(command.Role) ? null : command.Role.Trim() };
        members.Add(m);
        await members.SaveChangesAsync(ct);
        return new MemberDto(m.Id, m.ProjectId, m.UserId, m.Name, m.Role);
    }
}
