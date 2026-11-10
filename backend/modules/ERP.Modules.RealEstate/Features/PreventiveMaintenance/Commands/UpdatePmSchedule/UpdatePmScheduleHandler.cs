using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class UpdatePmScheduleHandler(IRepository<PmSchedule> schedules, PmScheduleInputRules inputs) : ICommandHandler<UpdatePmScheduleCommand, PmScheduleDto>
{
    public async Task<PmScheduleDto> Handle(UpdatePmScheduleCommand cmd, CancellationToken ct)
    {
        var s = await schedules.GetForUpdateAsync(cmd.Id, ct);
        if (cmd.Input.PropertyId != s.PropertyId) throw new FieldValidationException("propertyId", "A schedule cannot move to another property.");
        await inputs.MapAsync(cmd.Input, s, ct);
        await schedules.SaveChangesAsync(ct);
        return PmScheduleDto.From(s);
    }
}
