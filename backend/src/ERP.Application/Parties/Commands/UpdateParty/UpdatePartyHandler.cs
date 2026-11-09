using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;

namespace ERP.Application.Parties;

internal sealed class UpdatePartyHandler(IPartyRepository parties, ICustomFieldService fields, PartyMapping mapping, ICurrentUser user) : ICommandHandler<UpdatePartyCommand, PartyDto>
{
    public async Task<PartyDto> Handle(UpdatePartyCommand command, CancellationToken ct)
    {
        // 1. Load it, remember the sensitive values, check the group and copy the input.
        var p = await parties.GetForUpdateAsync(command.Id, ct);
        var (idNumber, iban) = (p.IdNumber, p.Iban);
        await mapping.EnsureGroupAsync(command.Input.PartyGroupId, ct);
        PartyMapping.Apply(command.Input, p);

        // 2. A user who only sees masked values must not overwrite the real ones with the mask.
        if (!user.HasPermission(CorePermissions.SensitiveRead)) { p.IdNumber = idNumber; p.Iban = iban; }

        // 3. Validate the custom fields and save.
        await fields.ApplyAsync(PartyMapping.EntityType, p, command.Input.CustomFields, isCreate: false, ct);
        await parties.SaveChangesAsync(ct);
        return mapping.ToDto(p, await fields.ReadAsync(PartyMapping.EntityType, p, ct));
    }
}
