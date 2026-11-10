using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Generates a draft payroll run (regular, one per entity and month, or off-cycle) with a line per employee on the payroll.</summary>
public sealed record GeneratePayrollCommand(GeneratePayrollInput Input) : ICommand<PayrollDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
