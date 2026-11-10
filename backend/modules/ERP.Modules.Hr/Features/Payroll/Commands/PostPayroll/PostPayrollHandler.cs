using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Platform;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class PostPayrollHandler(
    IPayrollRunRepository runs, IEmployeeRepository employees, IDepartmentRepository departmentRows, IAccountRepository accounts, IPayrollLoanRecoveryRepository recoveries, IEmployeeLoanRepository loanRows,
    IReadRepository<ApprovalRequest> approvalRequests, IApprovalService approvals, GratuityProvision provisions, ISettingsService settings, IPostingService posting, IUnitOfWork uow, PayrollSeparation separation)
    : ICommandHandler<PostPayrollCommand, PayrollDto>
{
    public Task<PayrollDto> Handle(PostPayrollCommand command, CancellationToken ct) => uow.InTransactionAsync(token => PostPayrollCoreAsync(command, token), ct);

    private async Task<PayrollDto> PostPayrollCoreAsync(PostPayrollCommand cmd, CancellationToken ct)
    {
        // 1. Only a draft with net pay; separation of duties when it is on.
        var run = await runs.GetWithLinesForUpdateAsync(cmd.Id, ct);
        if (run.Status is not (PayrollStatus.Draft or PayrollStatus.PendingApproval)) throw new ConflictException("Only a draft payroll can be posted.");
        PayrollRunRules.Totals(run); // the header follows the lines: a total saved earlier cannot unbalance the entry or the approval amount
        if (run.NetPay <= 0) throw new ConflictException("A payroll with no net pay cannot be posted.");
        await separation.RequireCanPostAsync(run, ct);

        // 2. When approval is required, submit it (or wait for the decision).
        if (await settings.GetAsync<bool?>("hr.payrollRequiresApproval", run.LegalEntityId, ct) == true)
        {
            var request = run.ApprovalRequestId is { } rid ? await approvalRequests.FindAsync(rid, ct) : null;
            if (request is null || request.Status is ApprovalStatus.Rejected or ApprovalStatus.Withdrawn)
            {
                var submitted = await approvals.SubmitAsync("payroll", run.Id, $"Payroll {run.Number} {run.Year}-{run.Month:00}", run.NetPay, null, run.LegalEntityId, ct);
                run.ApprovalRequestId = submitted.Id; run.Status = PayrollStatus.PendingApproval;
                await runs.SaveChangesAsync(ct);
                return PayrollDto.From(run);
            }
            if (request.Status == ApprovalStatus.Pending) throw new ConflictException("This payroll is waiting for approval.");
        }

        // 3. The accrual entry.
        var expense = await posting.AccountForRoleAsync("payroll.expense", ct);
        var payable = await posting.AccountForRoleAsync("payroll.accrued", ct);
        var memo = run.Kind == PayrollKind.FinalSettlement ? $"Final settlement {run.Number}" : $"Payroll accrual {run.Year}-{run.Month:00}";
        // debits and credits both come from the lines
        var loanTotal = run.Lines.Sum(l => l.LoanRecovery);
        var endOfService = run.Lines.Sum(l => l.EndOfService);
        var netPay = run.Lines.Sum(l => l.NetPay);
        var deductions = run.Lines.Sum(l => l.Deductions);
        var lines = new List<PostingLine>();
        // the expense is split by business unit (the unit of the employee's department); people without one stay on the plain line
        var unitByEmployee = await BusinessUnitsAsync(run.Lines.Select(l => l.EmployeeId).ToList(), ct);
        foreach (var g in run.Lines.GroupBy(l => unitByEmployee.GetValueOrDefault(l.EmployeeId)).OrderBy(g => g.Key))
        {
            var amount = g.Sum(l => l.GrossEarnings - l.EndOfService);
            if (amount > 0) lines.Add(new PostingLine(expense, amount, 0, memo, null, g.Key));
            else if (amount < 0) lines.Add(new PostingLine(expense, 0, -amount, memo, null, g.Key)); // a unit whose adjustments outweigh its pay reduces the expense; skipping it would unbalance the entry
        }
        if (endOfService > 0)
        {
            // end of service is paid out of the provision first; whatever the provision does not cover is expensed now
            var provisionAccount = await posting.AccountForRoleAsync("payroll.gratuity.provision", ct);
            var available = Math.Max(0m, await provisions.BalanceAsync(run.LegalEntityId, DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), ct));
            var fromProvision = Math.Min(endOfService, available);
            if (fromProvision > 0) lines.Add(new PostingLine(provisionAccount, fromProvision, 0, memo));
            if (endOfService - fromProvision > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync("payroll.gratuity.expense", ct), endOfService - fromProvision, 0, memo));
        }
        lines.Add(new PostingLine(payable, 0, netPay, memo));
        if (deductions - loanTotal > 0) lines.Add(new PostingLine(await DeductionsAccountAsync(ct), 0, deductions - loanTotal, memo));
        if (loanTotal > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync("payroll.advances", ct), 0, loanTotal, memo));
        var employerCost = run.Lines.Sum(l => l.EmployerCost);
        if (employerCost > 0)
        {
            lines.Add(new PostingLine(expense, employerCost, 0, memo + " (employer statutory cost)"));
            lines.Add(new PostingLine(await DeductionsAccountAsync(ct), 0, employerCost, memo + " (employer statutory cost)"));
        }

        // 4. Reduce the loans, post, and save.
        await ApplyLoanRecoveriesAsync(run, ct);
        var date = new DateOnly(run.Year, run.Month, DateTime.DaysInMonth(run.Year, run.Month));
        var journal = await posting.PostAsync(new PostingRequest(run.LegalEntityId, date, "payroll", run.Id, memo, run.Number, lines), ct);
        run.AccrualJournalId = journal.Id; run.Status = PayrollStatus.Posted; separation.StampPosted(run);
        await runs.SaveChangesAsync(ct);
        return PayrollDto.From(run);
    }

    /// <summary>Recurring deductions credit the "payroll.deductions" account when the workspace has one, otherwise the employee advances account (the original behaviour).</summary>
    private async Task<Guid> DeductionsAccountAsync(CancellationToken ct)
    {
        var id = await accounts.FindActiveIdForRoleAsync("payroll.deductions", ct);
        return id ?? await posting.AccountForRoleAsync("payroll.advances", ct);
    }

    private async Task<Dictionary<Guid, Guid?>> BusinessUnitsAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken ct)
    {
        var rows = await employees.ListDepartmentsAsync(employeeIds, ct);
        var deptIds = rows.Where(r => r.DepartmentId != null).Select(r => r.DepartmentId!.Value).Distinct().ToList();
        var units = deptIds.Count == 0 ? new Dictionary<Guid, Guid?>() : await departmentRows.BusinessUnitByIdAsync(deptIds, ct);
        return rows.ToDictionary(r => r.Id, r => r.DepartmentId is { } d ? units.GetValueOrDefault(d) : null);
    }

    /// <summary>Posting a run reduces the balance of every loan it recovers; a loan paid off becomes Settled.</summary>
    private async Task ApplyLoanRecoveriesAsync(PayrollRun run, CancellationToken ct)
    {
        var rows = await recoveries.ListUnappliedForRunForUpdateAsync(run.Id, ct);
        foreach (var group in rows.GroupBy(r => r.LoanId))
        {
            var loan = await loanRows.GetForUpdateAsync(group.Key, ct);
            var amount = group.Sum(r => r.Amount);
            if (loan.Status != LoanStatus.Active || amount > loan.Remaining) throw new ConflictException("A staff loan changed since this payroll was prepared. Recalculate the payroll.");
            loan.Remaining -= amount;
            if (loan.Remaining <= 0) loan.Status = LoanStatus.Settled;
            foreach (var r in group) r.Applied = true;
        }
    }
}
