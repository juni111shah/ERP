using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>What is ready to bill, project by project: unbilled time, achieved milestones, expenses and what is left of a fixed fee.</summary>
public sealed record GetBillingOverviewQuery() : IQuery<IReadOnlyList<ProjectBillingSummaryDto>>, IRequirePermission
{
    public string RequiredPermission => P.BillingRead;
}
