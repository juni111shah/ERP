using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Repositories;
using MediatR;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class DeleteBomHandler(IBillOfMaterialsRepository boms, IReadRepository<WorkOrder> workOrders) : ICommandHandler<DeleteBomCommand>
{
    public async Task<Unit> Handle(DeleteBomCommand request, CancellationToken cancellationToken)
    {
        // 1. A BOM that work orders were raised from stays; it can be deactivated instead.
        var bom = await boms.GetForUpdateAsync(request.Id, cancellationToken);
        if (await workOrders.AnyAsync(w => w.BomId == bom.Id, cancellationToken))
            throw new ConflictException("This BOM is used by work orders and cannot be deleted. Deactivate it instead.");

        // 2. Delete.
        boms.Remove(bom);
        await boms.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
