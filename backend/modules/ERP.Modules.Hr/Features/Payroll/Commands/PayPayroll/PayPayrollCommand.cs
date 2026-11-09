using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Records a salary payment (Dr accrued salaries, Cr bank) for the chosen employees, or for every unpaid line when none are given. The run is PartiallyPaid until every line is paid.</summary>
public sealed record PayPayrollCommand(Guid Id, Guid BankAccountId, DateOnly Date, IReadOnlyList<Guid>? EmployeeIds = null) : ICommand<PayrollDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => P.PayrollPay; }
