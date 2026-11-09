using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>A payroll run with its lines.</summary>
public sealed record GetPayrollQuery(Guid Id) : IQuery<PayrollDto>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
