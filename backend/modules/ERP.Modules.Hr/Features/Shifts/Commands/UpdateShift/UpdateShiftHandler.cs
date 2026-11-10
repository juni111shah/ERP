using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdateShiftHandler(IRepository<Shift> shifts, IEmployeeRepository employees) : ICommandHandler<UpdateShiftCommand, ShiftDto>
{
    public async Task<ShiftDto> Handle(UpdateShiftCommand command, CancellationToken ct)
    {
        // 1. Load it; a changed code must still be unique.
        var s = await shifts.GetForUpdateAsync(command.Id, ct);
        var code = command.Input.Code.Trim().ToUpperInvariant();
        if (code != s.Code && await shifts.AnyAsync(x => x.Code == code && x.Id != s.Id, ct)) throw new ConflictException($"A shift with code '{code}' already exists.");

        // 2. Fill and save; answer with how many employees are on it.
        ShiftMapping.Fill(s, command.Input, code);
        await shifts.SaveChangesAsync(ct);
        return ShiftDto.From(s, await employees.CountOnShiftAsync(s.Id, ct));
    }
}
