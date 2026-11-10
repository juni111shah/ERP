using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Reverses the accrual (and any payments) of a posted run. The reason is required (see <see cref="VoidMemo"/>); the month is free for a new Regular run afterwards.</summary>
public sealed record VoidPayrollCommand(Guid Id, string? Reason) : ICommand<PayrollDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => P.PayrollPost; }
