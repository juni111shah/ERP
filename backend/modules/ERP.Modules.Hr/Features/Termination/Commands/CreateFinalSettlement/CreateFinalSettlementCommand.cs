using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Creates the final-settlement payroll run (kind FinalSettlement, one line) for a terminated employee; it is then posted and paid like any run.</summary>
public sealed record CreateFinalSettlementCommand(Guid EmployeeId) : ICommand<PayrollDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
