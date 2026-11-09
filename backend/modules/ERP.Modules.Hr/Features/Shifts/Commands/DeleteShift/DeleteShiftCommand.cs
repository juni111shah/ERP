using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes a shift nobody is on.</summary>
public sealed record DeleteShiftCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
