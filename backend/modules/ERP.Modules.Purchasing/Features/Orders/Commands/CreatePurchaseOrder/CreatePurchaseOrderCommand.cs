using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Creates a draft purchase order.</summary>
public sealed record CreatePurchaseOrderCommand(PurchaseOrderInput Input) : ICommand<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
