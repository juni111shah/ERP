using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Creates a shift (start time, grace and hours) with a unique code.</summary>
public sealed record CreateShiftCommand(ShiftInput Input) : ICommand<ShiftDto>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
