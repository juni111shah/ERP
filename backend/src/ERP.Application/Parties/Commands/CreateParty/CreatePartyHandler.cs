using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Domain.Parties;

namespace ERP.Application.Parties;

internal sealed class CreatePartyHandler(IPartyRepository parties, INumberingService numbers, ICustomFieldService fields, PartyMapping mapping) : ICommandHandler<CreatePartyCommand, PartyDto>
{
    public async Task<PartyDto> Handle(CreatePartyCommand command, CancellationToken ct)
    {
        // 1. Number it, check the group and copy the input.
        var p = new Party { Code = await numbers.NextAsync("party", ct: ct) };
        await mapping.EnsureGroupAsync(command.Input.PartyGroupId, ct);
        PartyMapping.Apply(command.Input, p);

        // 2. Validate the custom fields and save.
        await fields.ApplyAsync(PartyMapping.EntityType, p, command.Input.CustomFields, isCreate: true, ct);
        parties.Add(p);
        await parties.SaveChangesAsync(ct);
        return mapping.ToDto(p, await fields.ReadAsync(PartyMapping.EntityType, p, ct));
    }
}
