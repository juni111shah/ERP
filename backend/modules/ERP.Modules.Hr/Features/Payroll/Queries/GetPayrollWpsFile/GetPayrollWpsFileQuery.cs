using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Salary file (WPS) for a posted or paid run, in the format of the employing entity's country; see Wps/. UnpaidOnly leaves out lines already paid.</summary>
public sealed record GetPayrollWpsFileQuery(Guid Id, bool UnpaidOnly = false) : IQuery<string>, IRequirePermission { public string RequiredPermission => P.PayrollPay; }
