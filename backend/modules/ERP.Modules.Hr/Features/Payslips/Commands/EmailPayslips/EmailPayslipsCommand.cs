using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Emails every payslip of a posted or paid run to its employee's email address; employees without one are skipped.</summary>
public sealed record EmailPayslipsCommand(Guid RunId) : ICommand<PayslipBulkEmailResult>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
