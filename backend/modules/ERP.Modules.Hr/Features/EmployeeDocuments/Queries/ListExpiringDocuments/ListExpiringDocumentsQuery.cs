using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Documents that expire within <c>Days</c> days or have already expired, soonest first. Staff who have left are left out.</summary>
public sealed record ListExpiringDocumentsQuery(int Days) : IQuery<IReadOnlyList<ExpiringDocumentDto>>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
