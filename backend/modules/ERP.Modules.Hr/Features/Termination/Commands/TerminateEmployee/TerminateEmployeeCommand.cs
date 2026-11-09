using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Ends the employment: status Terminated, date and reason stored, future-dated leave cancelled. Returns the settlement preview.</summary>
public sealed record TerminateEmployeeCommand(Guid EmployeeId, TerminateInput Input) : ICommand<SettlementPreviewDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
