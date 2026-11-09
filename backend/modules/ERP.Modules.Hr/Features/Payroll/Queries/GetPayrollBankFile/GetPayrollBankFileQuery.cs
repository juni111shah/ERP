using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>One CSV line per employee: number, name, IBAN and net pay, for the bank upload.</summary>
public sealed record GetPayrollBankFileQuery(Guid Id, bool UnpaidOnly = false) : IQuery<string>, IRequirePermission { public string RequiredPermission => P.PayrollPay; }
