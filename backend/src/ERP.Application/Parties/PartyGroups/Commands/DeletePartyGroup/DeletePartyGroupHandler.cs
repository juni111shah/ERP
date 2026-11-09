using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Parties;
using MediatR;

namespace ERP.Application.Parties;

internal sealed class DeletePartyGroupHandler(IRepository<PartyGroup> groups, IReadRepository<Party> parties) : ICommandHandler<DeletePartyGroupCommand>
{
    public async Task<Unit> Handle(DeletePartyGroupCommand command, CancellationToken ct)
    {
        // 1. Only a group without parties can go.
        var group = await groups.GetForUpdateAsync(command.Id, ct);
        if (await parties.AnyAsync(p => p.PartyGroupId == group.Id, ct)) throw new ConflictException("This group still has parties. Move them to another group first, or deactivate the group.");

        // 2. Remove and save.
        groups.Remove(group);
        await groups.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
