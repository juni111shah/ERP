using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Dashboard;

/// <summary>The owner's view: revenue, profit, cash, receivables, payables, branches, budget and module sections for a period.</summary>
public sealed record GetOwnerDashboardQuery(Guid? LegalEntityId, DateOnly? From, DateOnly? To) : IQuery<OwnerDashboardDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
