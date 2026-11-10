using ERP.Application.Common;
using ERP.Application.Printing;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>The payslip of one line of a posted or paid run, as a PDF file.</summary>
public sealed record GetPayslipPdfQuery(Guid RunId, Guid LineId) : IQuery<PrintPdfFile>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
