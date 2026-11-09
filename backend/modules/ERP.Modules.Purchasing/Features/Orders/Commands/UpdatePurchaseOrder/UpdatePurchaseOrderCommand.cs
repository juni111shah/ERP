using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Replaces a draft or submitted purchase order; any approval is released.</summary>
public sealed record UpdatePurchaseOrderCommand(Guid Id, PurchaseOrderInput Input) : ICommand<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
