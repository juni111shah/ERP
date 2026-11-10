using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Domain.Organizations;

namespace ERP.Application.Organizations;

internal sealed class UpdateBusinessUnitHandler(IRepository<BusinessUnit> businessUnits, ICustomFieldService fields) : ICommandHandler<UpdateBusinessUnitCommand, BusinessUnitDto>
{
    private const string BusinessUnitType = "businessunit";

    public async Task<BusinessUnitDto> Handle(UpdateBusinessUnitCommand command, CancellationToken ct)
    {
        var i = command.Input;
        var unit = await businessUnits.GetForUpdateAsync(command.Id, ct);
        if (i.LegalEntityId != unit.LegalEntityId) throw new FieldValidationException("legalEntityId", "A business unit cannot move to another legal entity.");
        var code = i.Code.Trim();
        if (code != unit.Code && await businessUnits.AnyAsync(b => b.LegalEntityId == unit.LegalEntityId && b.Code == code, ct))
            throw new ConflictException($"A business unit with code '{code}' exists in this legal entity.");
        unit.Code = code; unit.Name = i.Name.Trim(); unit.Address = i.Address; unit.IsActive = i.IsActive; if (!string.IsNullOrWhiteSpace(i.Kind)) unit.Kind = i.Kind.Trim().ToLowerInvariant();
        await fields.ApplyAsync(BusinessUnitType, unit, i.CustomFields, isCreate: false, ct);
        await businessUnits.SaveChangesAsync(ct);
        return BusinessUnitDto.From(unit, await fields.ReadAsync(BusinessUnitType, unit, ct));
    }
}
