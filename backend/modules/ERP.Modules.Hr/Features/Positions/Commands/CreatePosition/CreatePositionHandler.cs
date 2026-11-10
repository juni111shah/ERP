using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class CreatePositionHandler(IRepository<Position> positions) : ICommandHandler<CreatePositionCommand, PositionDto>
{
    public async Task<PositionDto> Handle(CreatePositionCommand command, CancellationToken ct)
    {
        // 1. The code is unique.
        var code = command.Input.Code.Trim().ToUpperInvariant();
        if (await positions.AnyAsync(p => p.Code == code, ct)) throw new ConflictException($"A position with code '{code}' already exists.");

        // 2. Fill, add and save.
        var p = new Position();
        PositionMapping.Fill(p, command.Input, code);
        positions.Add(p);
        await positions.SaveChangesAsync(ct);
        return PositionDto.From(p);
    }
}
