using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Every open task of every employee, soonest due first (the HR to-do list).</summary>
public sealed record ListOpenTasksQuery() : IQuery<IReadOnlyList<OpenTaskDto>>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
