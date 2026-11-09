using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>One purchase return with its lines and what it took off each bill.</summary>
public sealed record GetPurchaseReturnQuery(Guid Id) : IQuery<PurchaseReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsRead; }
