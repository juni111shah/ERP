using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes a roster entry.</summary>
public sealed record DeleteRosterCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
