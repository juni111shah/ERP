using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

internal sealed class EmailPayslipsHandler(PayslipDocuments payslips) : ICommandHandler<EmailPayslipsCommand, PayslipBulkEmailResult>
{
    public async Task<PayslipBulkEmailResult> Handle(EmailPayslipsCommand command, CancellationToken ct)
    {
        // 1. Email must be set up.
        payslips.EnsureEmailConfigured();
        var run = await payslips.LoadRunAsync(command.RunId, ct);

        // 2. One email per line; a line that cannot be sent is skipped with the reason.
        int sent = 0; var skipped = new List<string>();
        foreach (var line in run.Lines.OrderBy(l => l.EmployeeNumber))
        {
            var p = await payslips.BuildAsync(run, line, ct);
            if (string.IsNullOrWhiteSpace(p.Email)) { skipped.Add($"{p.EmployeeNumber} {p.EmployeeName}: no email address"); continue; }
            try { await payslips.SendAsync(run, p, p.Email, null, ct); sent++; }
            catch (Exception e) when (e is ConflictException or FieldValidationException) { skipped.Add($"{p.EmployeeNumber} {p.EmployeeName}: {e.Message}"); }
        }
        return new PayslipBulkEmailResult(sent, skipped.Count, skipped);
    }
}
