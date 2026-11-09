using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Money received from a customer: Dr bank / cash, Cr receivables; optionally settles invoices at the same time. What is not allocated stays as customer credit.</summary>
public sealed record CreateReceiptCommand(ReceiptInput Input) : ICommand<ReceiptDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReceiptsWrite;
}
