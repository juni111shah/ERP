using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Modules.RealEstate.Features;

internal sealed class RunPreventiveMaintenanceHandler(PmGenerator generator, IClock clock) : ICommandHandler<RunPreventiveMaintenanceCommand, PmRunResultDto>
{
    public async Task<PmRunResultDto> Handle(RunPreventiveMaintenanceCommand cmd, CancellationToken ct)
        => new(await generator.RunAsync(DateOnly.FromDateTime(clock.UtcNow), ct));
}
