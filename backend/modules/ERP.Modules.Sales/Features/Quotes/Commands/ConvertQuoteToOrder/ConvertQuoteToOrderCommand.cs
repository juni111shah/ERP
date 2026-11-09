using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>Turns a quote into a confirmed sales order (or returns the order it was already converted to).</summary>
public sealed record ConvertQuoteToOrderCommand(Guid Id) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
