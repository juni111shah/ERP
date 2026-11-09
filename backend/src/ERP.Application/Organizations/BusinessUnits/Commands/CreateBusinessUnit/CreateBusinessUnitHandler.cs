using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Domain.Organizations;

namespace ERP.Application.Organizations;

internal sealed class CreateBusinessUnitHandler(IRepository<BusinessUnit> businessUnits, ICustomFieldService fields, ILegalEntityGuard legalEntityGuard)
    : ICommandHandler<CreateBusinessUnitCommand, BusinessUnitDto>
{
    private const string BusinessUnitType = "businessunit";

    public async Task<BusinessUnitDto> Handle(CreateBusinessUnitCommand command, CancellationToken ct)
    {
        var i = command.Input;
        await legalEntityGuard.RequireAsync(i.LegalEntityId, ct);
        var unit = new BusinessUnit { LegalEntityId = i.LegalEntityId, Code = i.Code.Trim(), Name = i.Name.Trim(), Address = i.Address, IsActive = i.IsActive, Kind = string.IsNullOrWhiteSpace(i.Kind) ? "branch" : i.Kind.Trim().ToLowerInvariant() };
        await fields.ApplyAsync(BusinessUnitType, unit, i.CustomFields, isCreate: true, ct);
        businessUnits.Add(unit);
        await businessUnits.SaveChangesAsync(ct);
        return BusinessUnitDto.From(unit, await fields.ReadAsync(BusinessUnitType, unit, ct));
    }
}
