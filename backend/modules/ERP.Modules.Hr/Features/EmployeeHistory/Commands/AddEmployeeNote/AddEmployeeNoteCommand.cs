using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Adds a note to an employee's history.</summary>
public sealed record AddEmployeeNoteCommand(Guid EmployeeId, HistoryNoteInput Input) : ICommand<EmployeeHistoryDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
