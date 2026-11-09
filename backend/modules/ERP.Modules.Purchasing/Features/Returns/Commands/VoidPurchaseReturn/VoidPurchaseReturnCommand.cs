using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Reverses the journal, puts the goods back at the cost they left at and restores the order and bill balances.</summary>
public sealed record VoidPurchaseReturnCommand(Guid Id, string? Reason) : ICommand<PurchaseReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
