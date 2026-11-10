using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Deletes a draft or cancelled purchase order that has no goods receipts.</summary>
public sealed record DeletePurchaseOrderCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
