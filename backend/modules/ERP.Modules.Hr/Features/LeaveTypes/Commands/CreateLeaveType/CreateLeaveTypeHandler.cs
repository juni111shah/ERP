using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateLeaveTypeHandler(ILeaveTypeRepository types, LeavePolicyService policy) : ICommandHandler<CreateLeaveTypeCommand, LeaveTypeDto>
{
    public async Task<LeaveTypeDto> Handle(CreateLeaveTypeCommand command, CancellationToken ct)
    {
        // 1. The code is unique.
        var code = command.Input.Code.Trim().ToUpperInvariant();
        if (await types.AnyAsync(t => t.Code == code, ct)) throw new ConflictException($"A leave type with code '{code}' already exists.");

        // 2. Add it with its tiers and pay bands, and save.
        var t = new LeaveType();
        LeaveTypeMapping.Map(command.Input, t, code);
        types.Add(t);
        await policy.SaveTiersAsync(t.Id, command.Input.Tiers, ct);
        await policy.SaveBandsAsync(t.Id, command.Input.PayBands, ct);
        await types.SaveChangesAsync(ct);
        return LeaveTypeDto.From(t, command.Input.Tiers, command.Input.PayBands);
    }
}
