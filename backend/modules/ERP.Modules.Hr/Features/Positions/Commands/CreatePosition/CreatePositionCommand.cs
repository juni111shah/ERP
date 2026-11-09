using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Creates a position with a unique (upper-case) code.</summary>
public sealed record CreatePositionCommand(PositionInput Input) : ICommand<PositionDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
