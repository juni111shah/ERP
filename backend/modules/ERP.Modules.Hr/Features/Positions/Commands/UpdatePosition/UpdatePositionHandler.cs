using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdatePositionHandler(IRepository<Position> positions) : ICommandHandler<UpdatePositionCommand, PositionDto>
{
    public async Task<PositionDto> Handle(UpdatePositionCommand command, CancellationToken ct)
    {
        // 1. Load it; a changed code must still be unique.
        var p = await positions.GetForUpdateAsync(command.Id, ct);
        var code = command.Input.Code.Trim().ToUpperInvariant();
        if (code != p.Code && await positions.AnyAsync(x => x.Code == code && x.Id != p.Id, ct)) throw new ConflictException($"A position with code '{code}' already exists.");

        // 2. Fill and save.
        PositionMapping.Fill(p, command.Input, code);
        await positions.SaveChangesAsync(ct);
        return PositionDto.From(p);
    }
}
