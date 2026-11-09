using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Ticks a task off (who and when), or opens it again.</summary>
public sealed record SetEmployeeTaskDoneCommand(Guid TaskId, bool Done) : ICommand<EmployeeTaskDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
