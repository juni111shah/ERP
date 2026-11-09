using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Headcount, structure, movements, payroll cost, leave and absence of a period (this year to date by default).</summary>
public sealed record GetHrSummaryReportQuery(DateOnly? From, DateOnly? To, Guid? LegalEntityId) : IQuery<HrSummaryReportDto>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
