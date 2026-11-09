using ERP.Application.Common;
using ERP.Application.Printing;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Emails one payslip to the employee's address (or the one given).</summary>
public sealed record EmailPayslipCommand(Guid RunId, Guid LineId, PayslipEmailInput Input) : ICommand<EmailSentDto>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
