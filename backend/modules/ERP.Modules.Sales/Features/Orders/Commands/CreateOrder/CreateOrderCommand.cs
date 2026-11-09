using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>Creates a draft sales order.</summary>
public sealed record CreateOrderCommand(OrderInput Input) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
