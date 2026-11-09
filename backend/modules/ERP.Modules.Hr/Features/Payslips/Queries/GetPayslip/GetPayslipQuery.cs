using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>The payslip of one line of a posted or paid run.</summary>
public sealed record GetPayslipQuery(Guid RunId, Guid LineId) : IQuery<PayslipDto>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
