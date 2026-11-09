using ERP.Application.Common;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class GetWorkOrderByIdHandler(IWorkOrderRepository orders) : IQueryHandler<GetWorkOrderByIdQuery, WorkOrderDto?>
{
    public async Task<WorkOrderDto?> Handle(GetWorkOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var wo = await orders.FindWithDetailsAsync(request.Id, cancellationToken);
        return wo is null ? null : WorkOrderDto.From(wo);
    }
}
