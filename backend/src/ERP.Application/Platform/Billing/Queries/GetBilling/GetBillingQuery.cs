using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Platform;

/// <summary>A client's own view of its plan, usage, the plans it can move to and its invoices. Every read is filtered on the caller's workspace: these tables are not org-scoped.</summary>
public sealed record GetBillingQuery : IQuery<BillingOverview>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillingRead;
}
