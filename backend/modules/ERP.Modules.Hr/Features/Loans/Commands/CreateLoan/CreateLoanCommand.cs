using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Gives an employee a staff loan or salary advance, recovered in instalments by payroll once paid out.</summary>
public sealed record CreateLoanCommand(LoanInput Input) : ICommand<LoanDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
