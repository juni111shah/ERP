using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes a position no employee holds.</summary>
public sealed record DeletePositionCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
