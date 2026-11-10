using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Birthdays and work anniversaries of the employees within the next <c>Days</c> days.</summary>
public sealed record GetBirthdaysAnniversariesQuery(int Days, Guid? LegalEntityId) : IQuery<HrOccasionsDto>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
