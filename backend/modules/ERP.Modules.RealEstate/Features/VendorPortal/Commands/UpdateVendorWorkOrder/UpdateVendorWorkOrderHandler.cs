using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class UpdateVendorWorkOrderHandler(ICurrentUser user, IWorkOrderRepository workOrders, IClock clock) : ICommandHandler<UpdateVendorWorkOrderCommand, VendorWorkOrderDto>
{
    public async Task<VendorWorkOrderDto> Handle(UpdateVendorWorkOrderCommand cmd, CancellationToken ct)
    {
        // 1. The vendor's own order.
        var me = VendorPortalUser.Me(user);
        var w = await workOrders.FindOfVendorForUpdateAsync(cmd.Id, me, ct) ?? throw new NotFoundException();

        // 2. Apply the cost and move it (a move the lifecycle refuses is a conflict).
        var u = cmd.Update;
        if (u.ActualCost is { } cost) w.ActualCost = cost;
        try { w.MoveTo(u.Status, DateOnly.FromDateTime(clock.UtcNow), u.Notes); }
        catch (ERP.Domain.Common.DomainException e) { throw new ConflictException(e.Message); }

        // 3. Save.
        await workOrders.SaveChangesAsync(ct);
        return VendorWorkOrderDto.From(w);
    }
}
