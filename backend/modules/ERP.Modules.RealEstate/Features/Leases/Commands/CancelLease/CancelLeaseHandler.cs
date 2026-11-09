using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CancelLeaseHandler(IRepository<Lease> leases, LeaseDtoFactory factory) : ICommandHandler<CancelLeaseCommand, LeaseDto>
{
    public async Task<LeaseDto> Handle(CancelLeaseCommand cmd, CancellationToken ct)
    {
        var lease = await leases.GetForUpdateAsync(cmd.Id, ct);
        lease.Cancel();
        await leases.SaveChangesAsync(ct);
        return await factory.BuildAsync(lease, ct);
    }
}
