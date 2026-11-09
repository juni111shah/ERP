using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes a public holiday.</summary>
public sealed record DeleteHolidayCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }
