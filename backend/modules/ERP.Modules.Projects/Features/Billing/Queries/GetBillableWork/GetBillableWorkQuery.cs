using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Everything that could go on the next invoice of a project, line by line, so people can choose.</summary>
public sealed record GetBillableWorkQuery(Guid ProjectId) : IQuery<BillableWorkDto>, IRequirePermission
{
    public string RequiredPermission => P.BillingRead;
}
