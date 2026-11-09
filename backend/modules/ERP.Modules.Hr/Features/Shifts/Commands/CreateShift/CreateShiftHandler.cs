using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateShiftHandler(IRepository<Shift> shifts) : ICommandHandler<CreateShiftCommand, ShiftDto>
{
    public async Task<ShiftDto> Handle(CreateShiftCommand command, CancellationToken ct)
    {
        // 1. The code is unique.
        var code = command.Input.Code.Trim().ToUpperInvariant();
        if (await shifts.AnyAsync(s => s.Code == code, ct)) throw new ConflictException($"A shift with code '{code}' already exists.");

        // 2. Fill, add and save.
        var s = new Shift();
        ShiftMapping.Fill(s, command.Input, code);
        shifts.Add(s);
        await shifts.SaveChangesAsync(ct);
        return ShiftDto.From(s, 0);
    }
}
