using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Raises a draft return to the vendor against a purchase order or a bill.</summary>
public sealed record CreatePurchaseReturnCommand(PurchaseReturnInput Input) : ICommand<PurchaseReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
