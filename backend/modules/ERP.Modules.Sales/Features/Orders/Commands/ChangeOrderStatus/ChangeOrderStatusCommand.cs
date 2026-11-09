using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Sales.Domain;

namespace ERP.Modules.Sales.Features;

/// <summary>Moves a sales order to another status by hand (confirm, which checks credit, or cancel).</summary>
public sealed record ChangeOrderStatusCommand(Guid Id, SalesOrderStatus Status) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
