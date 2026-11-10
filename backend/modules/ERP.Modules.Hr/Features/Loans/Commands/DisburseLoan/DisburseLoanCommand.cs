using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Pays the loan out: Dr employee advances, Cr bank. Once per loan.</summary>
public sealed record DisburseLoanCommand(Guid Id, Guid BankAccountId, DateOnly Date) : ICommand<LoanDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => P.PayrollPay; }
