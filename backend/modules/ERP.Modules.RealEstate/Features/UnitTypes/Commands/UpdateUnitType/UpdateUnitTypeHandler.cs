using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class UpdateUnitTypeHandler(IRepository<UnitType> types) : ICommandHandler<UpdateUnitTypeCommand, UnitTypeDto>
{
    public async Task<UnitTypeDto> Handle(UpdateUnitTypeCommand cmd, CancellationToken ct)
    {
        // 1. Load it; a changed code must stay unique.
        var type = await types.GetForUpdateAsync(cmd.Id, ct);
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (code != type.Code && await types.AnyAsync(t => t.Code == code, ct)) throw new ConflictException($"A unit type with code '{code}' exists.");

        // 2. Apply and save.
        UnitTypeInputs.Map(cmd.Input, type, code);
        await types.SaveChangesAsync(ct);
        return UnitTypeDto.From(type);
    }
}
