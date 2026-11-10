using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>One staff loan or salary advance.</summary>
public sealed record GetLoanQuery(Guid Id) : IQuery<LoanDto>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
