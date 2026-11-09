using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Posts the accrual (Dr salaries, Cr salaries payable and employee advances). When the setting requires approval the run waits for a decision first.</summary>
public sealed record PostPayrollCommand(Guid Id) : ICommand<PayrollDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => P.PayrollPost; }
