using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Income (rent invoiced, net of credit notes) less property operating expenses (work order actual costs), with yield annualised against the indicative value. Billed and collected are both before tax.</summary>
public sealed record GetPortfolioPerformanceQuery(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<PerformanceDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
