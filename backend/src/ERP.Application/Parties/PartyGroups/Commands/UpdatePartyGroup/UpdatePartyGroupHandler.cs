using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Parties;

namespace ERP.Application.Parties;

internal sealed class UpdatePartyGroupHandler(IRepository<PartyGroup> groups) : ICommandHandler<UpdatePartyGroupCommand, PartyGroupDto>
{
    public async Task<PartyGroupDto> Handle(UpdatePartyGroupCommand command, CancellationToken ct)
    {
        // 1. Load it; a changed code must still be unique.
        var group = await groups.GetForUpdateAsync(command.Id, ct);
        var code = command.Input.Code.Trim().ToUpperInvariant();
        if (code != group.Code && await groups.AnyAsync(g => g.Code == code && g.Id != group.Id, ct)) throw new ConflictException($"A party group with code '{code}' exists.");

        // 2. Apply and save.
        group.Code = code; group.Name = command.Input.Name.Trim(); group.IsActive = command.Input.IsActive;
        await groups.SaveChangesAsync(ct);
        return PartyGroupDto.From(group);
    }
}
