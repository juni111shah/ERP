using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

internal sealed class GetOrderHandler(ISalesOrderRepository orders, ICustomFieldService fields) : IQueryHandler<GetOrderQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderQuery q, CancellationToken ct)
    {
        var order = await orders.FindWithLinesForUpdateAsync(q.Id, ct) ?? throw new NotFoundException($"Sales order '{q.Id}' not found.");
        return await OrderDto.FromAsync(order, fields, ct);
    }
}
