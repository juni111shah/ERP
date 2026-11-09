using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Deletes a goods receipt whose quantity is not billed: the stock goes back out and the order's received quantities go down.</summary>
public sealed record DeleteGoodsReceiptCommand(Guid Id) : ICommand, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => P.ReceiptsManage; }
