using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class AddPlatformTeamMemberHandler(IPlatformDirectory directory, IRepository<PlatformSetting> settings, PlatformLog log) : ICommandHandler<AddPlatformTeamMemberCommand, NewTeamMember>
{
    public async Task<NewTeamMember> Handle(AddPlatformTeamMemberCommand command, CancellationToken ct)
    {
        var added = await directory.AddTeamMemberAsync(command.Input.Email.Trim(), command.Input.FullName.Trim(), ct);
        log.Add("Team member added", null, null, added.Member.Email);
        await settings.SaveChangesAsync(ct);
        return added;
    }
}
