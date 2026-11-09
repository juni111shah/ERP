using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Parties;

namespace ERP.Application.Parties;

internal sealed class CreatePartyGroupHandler(IRepository<PartyGroup> groups) : ICommandHandler<CreatePartyGroupCommand, PartyGroupDto>
{
    public async Task<PartyGroupDto> Handle(CreatePartyGroupCommand command, CancellationToken ct)
    {
        // 1. The code is unique.
        var code = command.Input.Code.Trim().ToUpperInvariant();
        if (await groups.AnyAsync(g => g.Code == code, ct)) throw new ConflictException($"A party group with code '{code}' exists.");

        // 2. Add and save.
        var group = new PartyGroup { Code = code, Name = command.Input.Name.Trim(), IsActive = command.Input.IsActive };
        groups.Add(group);
        await groups.SaveChangesAsync(ct);
        return PartyGroupDto.From(group);
    }
}
