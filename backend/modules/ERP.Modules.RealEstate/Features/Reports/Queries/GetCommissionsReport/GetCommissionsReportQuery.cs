using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Brokerage commission per agent for deals in the period: gross, agent, team lead and company shares, and what was paid out.</summary>
public sealed record GetCommissionsReportQuery(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<CommissionsDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
