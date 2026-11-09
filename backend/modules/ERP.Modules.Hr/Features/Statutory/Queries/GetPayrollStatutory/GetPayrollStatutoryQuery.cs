using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Per rule: how many employees, the base and the amount of a payroll run (what is filed with the authority).</summary>
public sealed record GetPayrollStatutoryQuery(Guid RunId) : IQuery<IReadOnlyList<StatutoryTotalDto>>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
