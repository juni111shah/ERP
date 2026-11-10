using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>Replaces a draft sales order, or a confirmed one with nothing delivered (its credit is checked again).</summary>
public sealed record UpdateOrderCommand(Guid Id, OrderInput Input) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
