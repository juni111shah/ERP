using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetWorkOrderHandler(IReadRepository<WorkOrder> workOrders, ICustomFieldService fields) : IQueryHandler<GetWorkOrderQuery, WorkOrderDto>
{
    public async Task<WorkOrderDto> Handle(GetWorkOrderQuery q, CancellationToken ct)
    {
        var workOrder = await workOrders.GetAsync(q.Id, ct);
        return WorkOrderDto.From(workOrder, await fields.ReadAsync(Docs.WorkOrder, workOrder, ct));
    }
}
