using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class RemoveMemberHandler(IProjectMemberRepository members, ProjectAccess access) : ICommandHandler<RemoveMemberCommand>
{
    public async Task<Unit> Handle(RemoveMemberCommand command, CancellationToken ct)
    {
        var member = await members.GetForUpdateAsync(command.Id, ct);
        await access.EnsureProjectAsync(member.ProjectId, ct);
        members.Remove(member);
        await members.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
