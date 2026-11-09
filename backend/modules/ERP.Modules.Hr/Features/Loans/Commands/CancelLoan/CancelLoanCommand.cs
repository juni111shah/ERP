using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Stops recovery. The unrecovered balance stays on the loan for the record; instalments already in a posted run are not undone.</summary>
public sealed record CancelLoanCommand(Guid Id) : ICommand<LoanDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
