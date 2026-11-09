using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Rebuilds the lines of a draft run from the current employees, attendance and leave, keeping manual adjustments.</summary>
public sealed record RecalculatePayrollCommand(Guid Id) : ICommand<PayrollDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
