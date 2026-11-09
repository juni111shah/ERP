using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Adds a public holiday (one per date).</summary>
public sealed record CreateHolidayCommand(HolidayInput Input) : ICommand<HolidayDto>, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }
