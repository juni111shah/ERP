using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Employees list. Pay columns are projected only for callers with <c>hr.salary.read</c>; the rest see null. "On leave today" comes from approved leave covering today.</summary>
public sealed record ListEmployeesQuery(PagedQuery Paging) : IQuery<PagedResult<EmployeeSummaryDto>>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
