using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>The department tree with its members and the reporting lines of everyone not terminated.</summary>
public sealed record GetOrgChartQuery() : IQuery<OrgChartDto>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
