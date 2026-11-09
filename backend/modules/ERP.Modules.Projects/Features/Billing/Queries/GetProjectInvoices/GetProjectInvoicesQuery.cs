using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>The invoices raised from a project.</summary>
public sealed record GetProjectInvoicesQuery(Guid ProjectId) : IQuery<IReadOnlyList<ProjectInvoiceDto>>, IRequirePermission
{
    public string RequiredPermission => P.BillingRead;
}
