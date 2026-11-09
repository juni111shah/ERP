using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>One purchase order with its lines and the bills raised from it.</summary>
public sealed record GetPurchaseOrderQuery(Guid Id) : IQuery<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersRead; }
