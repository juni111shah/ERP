using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class FlagHoldingOverHandler(IRepository<Lease> leases, LeaseDtoFactory factory) : ICommandHandler<FlagHoldingOverCommand, LeaseDto>
{
    public async Task<LeaseDto> Handle(FlagHoldingOverCommand cmd, CancellationToken ct)
    {
        var lease = await leases.GetForUpdateAsync(cmd.Id, ct);
        if (!lease.IsOccupying) throw new ConflictException("Only an active lease can be flagged as holding over.");
        lease.HoldingOverFlag = cmd.HoldingOver;
        await leases.SaveChangesAsync(ct);
        return await factory.BuildAsync(lease, ct);
    }
}
