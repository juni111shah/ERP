using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Receives goods against an approved purchase order: stock comes in and the order's received quantities go up.</summary>
public sealed record CreateGoodsReceiptCommand(GoodsReceiptInput Input) : ICommand<GoodsReceiptDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => P.ReceiptsManage; }
