using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Adds the country pack's leave types (by code) and fixed-date public holidays (by date) that are missing. Safe to repeat.</summary>
public sealed record ApplyLeaveCountryDefaultsCommand(int Year, Guid? LegalEntityId) : ICommand<LeaveDefaultsResult>, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }
