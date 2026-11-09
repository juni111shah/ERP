using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Platform;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class VoidPayrollHandler(
    IPayrollRunRepository runs, IJournalEntryRepository journals, IPayrollLoanRecoveryRepository recoveries, IRepository<EmployeeLoan> loanRows, IRepository<AuditEvent> audit, ICurrentUser user, IClock clock,
    IPostingService posting, IUnitOfWork uow) : ICommandHandler<VoidPayrollCommand, PayrollDto>
{
    public Task<PayrollDto> Handle(VoidPayrollCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Only a posted run with an accrual entry.
        var cmd = command;
        var run = await runs.GetWithLinesForUpdateAsync(cmd.Id, token);
        if (run.Status == PayrollStatus.Voided) throw new ConflictException("This payroll is already voided.");
        if (run.Status is PayrollStatus.Draft or PayrollStatus.PendingApproval) throw new ConflictException("Only a posted payroll can be voided. Delete a draft payroll instead.");
        if (run.AccrualJournalId is not { } accrualId) throw new ConflictException("This payroll has no accrual entry to reverse.");

        // 2. payments first (one journal per pay action), then the accrual; each reversal is dated today or later, like an invoice void
        var paymentIds = run.Lines.Where(l => l.PaymentJournalId is not null).Select(l => l.PaymentJournalId!.Value).ToHashSet();
        if (run.PaymentJournalId is { } last) paymentIds.Add(last);
        var memo = VoidMemo.For(run.Number, cmd.Reason);
        foreach (var id in paymentIds)
            await posting.ReverseTodayAsync(await journals.GetWithLinesForUpdateAsync(id, token), memo, token);
        await posting.ReverseTodayAsync(await journals.GetWithLinesForUpdateAsync(accrualId, token), memo, token);

        // 3. loan instalments this run recovered go back on the loans
        foreach (var rec in await recoveries.ListAppliedForRunForUpdateAsync(run.Id, token))
        {
            var loan = await loanRows.GetForUpdateAsync(rec.LoanId, token);
            loan.Remaining += rec.Amount;
            if (loan.Status == LoanStatus.Settled) loan.Status = LoanStatus.Active;
            rec.Applied = false;
        }

        // 4. Void, stamp, audit and save.
        run.Status = PayrollStatus.Voided;
        VoidMemo.Stamp(run, cmd.Reason, clock, user);
        VoidMemo.Audit(audit, user, clock, nameof(PayrollRun), run.Id, run.Number, cmd.Reason!);
        await runs.SaveChangesAsync(token);
        return PayrollDto.From(run);
    }, ct);
}
