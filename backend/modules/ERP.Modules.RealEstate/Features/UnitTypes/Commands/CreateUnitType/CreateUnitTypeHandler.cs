using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreateUnitTypeHandler(IRepository<UnitType> types) : ICommandHandler<CreateUnitTypeCommand, UnitTypeDto>
{
    public async Task<UnitTypeDto> Handle(CreateUnitTypeCommand cmd, CancellationToken ct)
    {
        // 1. The code is unique.
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (await types.AnyAsync(t => t.Code == code, ct)) throw new ConflictException($"A unit type with code '{code}' exists.");

        // 2. Create and save.
        var type = new UnitType();
        UnitTypeInputs.Map(cmd.Input, type, code);
        types.Add(type);
        await types.SaveChangesAsync(ct);
        return UnitTypeDto.From(type);
    }
}
