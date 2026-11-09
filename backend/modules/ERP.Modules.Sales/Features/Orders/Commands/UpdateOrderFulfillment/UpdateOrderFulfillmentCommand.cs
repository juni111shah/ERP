using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>
/// Records deliveries on a confirmed or processing order and issues the stock.
/// DeliveredQuantity is cumulative. DeliveryDate defaults to today and WarehouseId to the legal entity's first active warehouse.
/// </summary>
public sealed record UpdateOrderFulfillmentCommand(Guid Id, List<OrderFulfillmentLineInput> Lines, DateOnly? DeliveryDate = null, Guid? WarehouseId = null) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
