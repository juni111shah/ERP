using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreatePmScheduleHandler(IRepository<PmSchedule> schedules, PmScheduleInputRules inputs) : ICommandHandler<CreatePmScheduleCommand, PmScheduleDto>
{
    public async Task<PmScheduleDto> Handle(CreatePmScheduleCommand cmd, CancellationToken ct)
    {
        var s = new PmSchedule();
        await inputs.MapAsync(cmd.Input, s, ct);
        schedules.Add(s);
        await schedules.SaveChangesAsync(ct);
        return PmScheduleDto.From(s);
    }
}
