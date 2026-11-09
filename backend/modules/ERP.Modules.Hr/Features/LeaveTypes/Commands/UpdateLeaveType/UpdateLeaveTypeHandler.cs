using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdateLeaveTypeHandler(ILeaveTypeRepository types, LeavePolicyService policy) : ICommandHandler<UpdateLeaveTypeCommand, LeaveTypeDto>
{
    public async Task<LeaveTypeDto> Handle(UpdateLeaveTypeCommand command, CancellationToken ct)
    {
        // 1. Load it; a changed code must still be unique.
        var t = await types.GetForUpdateAsync(command.Id, ct);
        var code = command.Input.Code.Trim().ToUpperInvariant();
        if (code != t.Code && await types.AnyAsync(x => x.Code == code, ct)) throw new ConflictException($"A leave type with code '{code}' already exists.");

        // 2. Apply, save the tiers and pay bands, and save.
        LeaveTypeMapping.Map(command.Input, t, code);
        await policy.SaveTiersAsync(t.Id, command.Input.Tiers, ct);
        await policy.SaveBandsAsync(t.Id, command.Input.PayBands, ct);
        await types.SaveChangesAsync(ct);
        return LeaveTypeDto.From(t, command.Input.Tiers ?? (await policy.TiersAsync(t.Id, ct)).Select(x => new LeaveTierInput(x.FromServiceYears, x.Days)).ToList(), command.Input.PayBands ?? (await policy.BandsAsync(t.Id, ct)).Select(x => new LeavePayBandInput(x.FromDay, x.PayPercent)).ToList());
    }
}
