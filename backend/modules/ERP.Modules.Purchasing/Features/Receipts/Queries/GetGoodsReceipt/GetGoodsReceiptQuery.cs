using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>One goods receipt with its lines.</summary>
public sealed record GetGoodsReceiptQuery(Guid Id) : IQuery<GoodsReceiptDto>, IRequirePermission { public string RequiredPermission => P.ReceiptsRead; }
