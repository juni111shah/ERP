using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>Deletes a sales order that is not invoiced and has nothing delivered.</summary>
public sealed record DeleteOrderCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
