using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

/// <summary>Rules shared by the handlers that change a draft payroll run: load the draft, withdraw a pending approval, clear the loan and statutory rows, total the header.</summary>
public sealed class PayrollRunRules(
    IPayrollRunRepository runs, IReadRepository<ApprovalRequest> approvalRequests, IApprovalService approvals, IPayrollLoanRecoveryRepository recoveries, IPayrollLineStatutoryRepository statutoryRows)
{
    public async Task<PayrollRun> LoadDraftAsync(Guid id, CancellationToken ct)
    {
        var run = await runs.GetWithLinesForUpdateAsync(id, ct);
        if (run.Status is not (PayrollStatus.Draft or PayrollStatus.PendingApproval)) throw new ConflictException("Only a draft payroll can be changed.");
        return run;
    }

    /// <summary>A change invalidates a request still waiting for a decision; a decided one only loses its link when the run is recalculated.</summary>
    public async Task WithdrawPendingAsync(PayrollRun run, CancellationToken ct)
    {
        if (run.ApprovalRequestId is not { } rid) return;
        var request = await approvalRequests.FindAsync(rid, ct);
        if (request?.Status == ApprovalStatus.Pending) await approvals.WithdrawAsync(rid, ct);
    }

    public async Task RemoveStatutoryAsync(Guid runId, CancellationToken ct)
    {
        foreach (var r in await statutoryRows.ListForRunForUpdateAsync(runId, ct)) statutoryRows.Remove(r);
    }

    public async Task RemoveRecoveriesAsync(Guid runId, CancellationToken ct)
    {
        foreach (var r in await recoveries.ListForRunForUpdateAsync(runId, ct)) recoveries.Remove(r);
    }

    public static void Totals(PayrollRun run)
    {
        run.Employees = run.Lines.Count; run.EmployerCost = run.Lines.Sum(l => l.EmployerCost); run.GrossEarnings = run.Lines.Sum(l => l.GrossEarnings); run.Deductions = run.Lines.Sum(l => l.Deductions); run.NetPay = run.Lines.Sum(l => l.NetPay);
    }
}
