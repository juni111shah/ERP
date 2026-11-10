using ERP.Application.Common;

namespace ERP.Modules.RealEstate.Features;

internal sealed class ActivateLeaseHandler(LeaseActivation activation) : ICommandHandler<ActivateLeaseCommand, LeaseDto>
{
    public Task<LeaseDto> Handle(ActivateLeaseCommand cmd, CancellationToken ct) => activation.ActivateAsync(cmd.Id, ct);
}
