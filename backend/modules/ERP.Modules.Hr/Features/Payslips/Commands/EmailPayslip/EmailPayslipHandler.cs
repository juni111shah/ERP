using ERP.Application.Common;
using ERP.Application.Printing;

namespace ERP.Modules.Hr.Features;

internal sealed class EmailPayslipHandler(PayslipDocuments payslips) : ICommandHandler<EmailPayslipCommand, EmailSentDto>
{
    public async Task<EmailSentDto> Handle(EmailPayslipCommand command, CancellationToken ct)
    {
        // 1. Email must be set up; build the payslip.
        payslips.EnsureEmailConfigured();
        var run = await payslips.LoadRunAsync(command.RunId, ct);
        var p = await payslips.BuildAsync(run, run.Lines.FirstOrDefault(l => l.Id == command.LineId) ?? throw new NotFoundException(), ct);

        // 2. Send it to the address given, else the employee's.
        var to = string.IsNullOrWhiteSpace(command.Input.To) ? p.Email : command.Input.To;
        if (string.IsNullOrWhiteSpace(to)) throw new FieldValidationException("to", "This employee has no email address. Enter one to send to.");
        return await payslips.SendAsync(run, p, to, command.Input.Message, ct);
    }
}
