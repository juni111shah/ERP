using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Moves or renames a public holiday; the date stays unique.</summary>
public sealed record UpdateHolidayCommand(Guid Id, HolidayInput Input) : ICommand<HolidayDto>, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }
