using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>One sales order with its lines.</summary>
public sealed record GetOrderQuery(Guid Id) : IQuery<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersRead; }
