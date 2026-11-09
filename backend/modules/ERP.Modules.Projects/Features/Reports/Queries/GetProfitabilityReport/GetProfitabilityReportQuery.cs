using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Billed and unbilled value against cost per project, for an open-ended period; the contract margin only over the whole life.</summary>
public sealed record GetProfitabilityReportQuery(DateOnly? From, DateOnly? To, Guid? ProjectId) : IQuery<IReadOnlyList<ProfitabilityRow>>, IRequirePermission
{
    public string RequiredPermission => P.ReportsRead;
}
