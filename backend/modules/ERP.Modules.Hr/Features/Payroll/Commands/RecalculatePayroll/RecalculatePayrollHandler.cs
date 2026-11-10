using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class RecalculatePayrollHandler(
    IPayrollRunRepository runs, IRepository<PayrollLine> lineRows, IPayrollLoanRecoveryRepository recoveries, IPayrollLineStatutoryRepository statutoryRows, PayrollRunRules rules, PayrollLineBuilder builder,
    IUnitOfWork uow, PayrollSeparation separation) : ICommandHandler<RecalculatePayrollCommand, PayrollDto>
{
    public Task<PayrollDto> Handle(RecalculatePayrollCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Load the draft.
        var run = await rules.LoadDraftAsync(command.Id, token);
        // A settlement holds one person and amounts worked out when they left; rebuilding it as a normal run would add every employee and lose them.
        if (run.Kind == PayrollKind.FinalSettlement) throw new ConflictException("A final settlement cannot be recalculated. Delete the draft and prepare the settlement again.");
        await rules.WithdrawPendingAsync(run, token);

        // 2. Drop the old lines (keeping the adjustments), loan recoveries and statutory rows.
        var keep = run.Lines.Where(l => l.Adjustment != 0).ToDictionary(l => l.EmployeeId, l => (l.Adjustment, l.AdjustmentNote));
        foreach (var old in run.Lines.ToList()) lineRows.Remove(old);
        await rules.RemoveRecoveriesAsync(run.Id, token);
        await rules.RemoveStatutoryAsync(run.Id, token);

        // 3. Build fresh lines, back to draft, and save.
        var (fresh, recs, stats) = await builder.BuildLinesAsync(run, keep, run.Kind == PayrollKind.OffCycle ? run.Lines.Select(l => l.EmployeeId).ToHashSet() : null, token);
        lineRows.AddRange(fresh);
        recoveries.AddRange(recs.Select(r => { r.PayrollRunId = run.Id; return r; }));
        statutoryRows.AddRange(stats);
        run.Lines.Clear(); run.Lines.AddRange(fresh); PayrollRunRules.Totals(run); run.Status = PayrollStatus.Draft; run.ApprovalRequestId = null; separation.StampAdjusted(run);
        await runs.SaveChangesAsync(token);
        return PayrollDto.From(run);
    }, ct);
}
