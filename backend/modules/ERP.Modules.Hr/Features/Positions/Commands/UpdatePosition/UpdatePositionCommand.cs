using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Updates a position; a changed code stays unique.</summary>
public sealed record UpdatePositionCommand(Guid Id, PositionInput Input) : ICommand<PositionDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
