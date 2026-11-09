using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class GetPurchaseOrderHandler(IPurchaseOrderRepository orders, IPurchaseOrderBillLineRepository billLinks, ICustomFieldService fields)
    : IQueryHandler<GetPurchaseOrderQuery, PurchaseOrderDto>
{
    public async Task<PurchaseOrderDto> Handle(GetPurchaseOrderQuery q, CancellationToken ct)
    {
        var order = await orders.FindWithLinesForUpdateAsync(q.Id, ct) ?? throw new NotFoundException($"Purchase order '{q.Id}' not found.");
        var billIds = await billLinks.ListBillIdsOfOrderAsync(order.Id, ct);
        return await PurchaseOrderDto.FromAsync(order, fields, ct) with { BillIds = billIds };
    }
}
