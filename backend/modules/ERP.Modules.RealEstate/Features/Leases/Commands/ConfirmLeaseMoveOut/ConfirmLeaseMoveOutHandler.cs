using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

internal sealed class ConfirmLeaseMoveOutHandler(IRepository<Lease> leases, IRepository<Unit> units, IUnitOfWork unitOfWork, LeaseDtoFactory factory)
    : ICommandHandler<ConfirmLeaseMoveOutCommand, LeaseDto>
{
    private DateOnly Today => factory.Today;

    public Task<LeaseDto> Handle(ConfirmLeaseMoveOutCommand cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var lease = await leases.GetForUpdateAsync(cmd.Id, token);
        lease.ConfirmMoveOut();
        var unit = await units.GetForUpdateAsync(lease.UnitId, token);
        if (unit.Status == UnitStatus.Occupied) unit.Vacate(Today);
        await leases.SaveChangesAsync(token);
        return await factory.BuildAsync(lease, token);
    }, ct);
}
