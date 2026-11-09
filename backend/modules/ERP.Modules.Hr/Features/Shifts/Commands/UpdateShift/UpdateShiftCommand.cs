using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Updates a shift; a changed code stays unique.</summary>
public sealed record UpdateShiftCommand(Guid Id, ShiftInput Input) : ICommand<ShiftDto>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
