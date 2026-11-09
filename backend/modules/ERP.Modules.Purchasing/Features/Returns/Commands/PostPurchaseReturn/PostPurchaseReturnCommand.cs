using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>
/// Posts a draft purchase return.
/// Goods not billed yet: Dr goods-received clearing, Cr inventory, and the order simply has less received. Goods already billed: Dr payables (with the vendor), Cr inventory and recoverable input tax,
/// and the bill's balance goes down. A gap between stock cost and the amount cleared goes to purchase variance.
/// </summary>
public sealed record PostPurchaseReturnCommand(Guid Id) : ICommand<PurchaseReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
