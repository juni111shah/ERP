using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class AdjustPayrollLineHandler(IPayrollRunRepository runs, PayrollRunRules rules, PayrollSeparation separation) : ICommandHandler<AdjustPayrollLineCommand, PayrollDto>
{
    public async Task<PayrollDto> Handle(AdjustPayrollLineCommand command, CancellationToken ct)
    {
        // 1. Load the draft and the line.
        var run = await rules.LoadDraftAsync(command.RunId, ct);
        await rules.WithdrawPendingAsync(run, ct);
        var line = run.Lines.FirstOrDefault(l => l.Id == command.LineId) ?? throw new NotFoundException();

        // 2. The pay may not go negative.
        var (gross, net) = PayrollMath.WithAdjustment(line.Basic, line.Allowances, line.Overtime, line.Absence, command.Adjustment, line.Deductions, line.LeaveEncashment + line.EndOfService);
        if (gross < 0 || net < 0) throw new FieldValidationException("adjustment", "The adjustment would make the pay negative.");

        // 3. Apply, back to draft, and save.
        line.Adjustment = command.Adjustment; line.AdjustmentNote = command.Note?.Trim(); line.GrossEarnings = gross; line.NetPay = net;
        PayrollRunRules.Totals(run); run.Status = PayrollStatus.Draft; run.ApprovalRequestId = null; separation.StampAdjusted(run);
        await runs.SaveChangesAsync(ct);
        return PayrollDto.From(run);
    }
}
