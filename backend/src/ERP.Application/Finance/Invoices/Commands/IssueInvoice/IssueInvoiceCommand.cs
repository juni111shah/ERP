using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Issues a draft: gives it its number and posts it to the ledger. The answer carries informational warnings.</summary>
public sealed record IssueInvoiceCommand(Guid Id) : ICommand<InvoiceDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.InvoicesIssue;
}
