using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Moves a purchase order to another status by hand (submit, approve through the approval gate, cancel, back to draft).</summary>
public sealed record ChangePurchaseOrderStatusCommand(Guid Id, PurchaseOrderStatus Status) : ICommand<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
