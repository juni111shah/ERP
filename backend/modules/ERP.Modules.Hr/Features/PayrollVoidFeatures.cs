using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Platform;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

/// <summary>Records a salary payment (Dr accrued salaries, Cr bank) for the chosen employees, or for every unpaid line when none are given. The run is PartiallyPaid until every line is paid.</summary>
public sealed record PayPayroll(Guid Id, Guid BankAccountId, DateOnly Date, IReadOnlyList<Guid>? EmployeeIds = null) : ICommand<PayrollDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => P.PayrollPay; }

/// <summary>Reverses the accrual (and any payments) of a posted run. The reason is required (see <see cref="VoidMemo"/>); the month is free for a new Regular run afterwards.</summary>
public sealed record VoidPayroll(Guid Id, string? Reason) : ICommand<PayrollDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => P.PayrollPost; }

public sealed class PayPayrollValidator : AbstractValidator<PayPayroll>
{
    public PayPayrollValidator()
    {
        RuleFor(x => x.BankAccountId).NotEmpty().WithMessage("Choose the bank account.");
        RuleFor(x => x.Date).MustBePlausibleDate();
    }
}

public sealed class VoidPayrollValidator : AbstractValidator<VoidPayroll>
{
    public VoidPayrollValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}

internal sealed class PayrollSettlementHandlers(
    IRepository<PayrollRun> runs, IRepository<JournalEntry> journals, IReadRepository<Account> accounts, IRepository<PayrollLoanRecovery> recoveries, IRepository<EmployeeLoan> loanRows, IRepository<AuditEvent> audit, ICurrentUser user, IClock clock,
    IPostingService posting, IUnitOfWork uow, PayrollSeparation separation) :
    IRequestHandler<PayPayroll, PayrollDto>,
    IRequestHandler<VoidPayroll, PayrollDto>
{
    public Task<PayrollDto> Handle(PayPayroll cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var run = await LoadAsync(cmd.Id, token);
        if (run.Status is PayrollStatus.Paid) throw new ConflictException("This payroll is already paid.");
        if (run.Status == PayrollStatus.Voided) throw new ConflictException("This payroll was voided.");
        if (run.Status is not (PayrollStatus.Posted or PayrollStatus.PartiallyPaid)) throw new ConflictException("Post the payroll before paying it.");
        await separation.RequireCanPayAsync(run, token);
        var bank = await accounts.FindAsync(cmd.BankAccountId, token);
        if (bank is null || !BankAccountRules.IsCashOrBank(bank)) throw new FieldValidationException("bankAccountId", "Choose an active bank account.");
        if (cmd.Date < new DateOnly(run.Year, run.Month, 1)) throw new FieldValidationException("date", "The payment date cannot be before the payroll month.");

        List<PayrollLine> selected;
        if (cmd.EmployeeIds is { Count: > 0 } wanted)
        {
            selected = [];
            foreach (var employeeId in wanted.Distinct())
            {
                var line = run.Lines.FirstOrDefault(l => l.EmployeeId == employeeId) ?? throw new FieldValidationException("employeeIds", "One of the employees is not on this payroll.");
                if (line.PaidOn is not null) throw new ConflictException($"{line.EmployeeName} is already paid.");
                selected.Add(line);
            }
        }
        else selected = run.Lines.Where(l => l.PaidOn is null).ToList();
        if (selected.Count == 0) throw new ConflictException("There is nothing left to pay on this payroll.");

        var amount = selected.Sum(l => l.NetPay);
        if (amount <= 0) throw new ConflictException("The selected lines have no net pay to pay.");
        var payable = await posting.AccountForRoleAsync("payroll.accrued", token);
        var memo = $"Salary payment {run.Year}-{run.Month:00}";
        var journal = await posting.PostAsync(new PostingRequest(run.LegalEntityId, cmd.Date, "payroll", run.Id, memo, run.Number,
            [new PostingLine(payable, amount, 0, memo), new PostingLine(bank.Id, 0, amount, memo)]), token);
        foreach (var l in selected) { l.PaidOn = cmd.Date; l.PaymentJournalId = journal.Id; }
        run.PaymentJournalId = journal.Id; run.PaidOn = cmd.Date;
        run.Status = run.Lines.All(l => l.PaidOn is not null) ? PayrollStatus.Paid : PayrollStatus.PartiallyPaid;
        await runs.SaveChangesAsync(token);
        return PayrollHandlers.ToDto(run);
    }, ct);

    public Task<PayrollDto> Handle(VoidPayroll cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var run = await LoadAsync(cmd.Id, token);
        if (run.Status == PayrollStatus.Voided) throw new ConflictException("This payroll is already voided.");
        if (run.Status is PayrollStatus.Draft or PayrollStatus.PendingApproval) throw new ConflictException("Only a posted payroll can be voided. Delete a draft payroll instead.");
        if (run.AccrualJournalId is not { } accrualId) throw new ConflictException("This payroll has no accrual entry to reverse.");

        // payments first (one journal per pay action), then the accrual; each reversal is dated today or later, like an invoice void
        var paymentIds = run.Lines.Where(l => l.PaymentJournalId is not null).Select(l => l.PaymentJournalId!.Value).ToHashSet();
        if (run.PaymentJournalId is { } last) paymentIds.Add(last);
        var memo = VoidMemo.For(run.Number, cmd.Reason);
        foreach (var id in paymentIds)
            await posting.ReverseTodayAsync(await JournalAsync(id, token), memo, token);
        await posting.ReverseTodayAsync(await JournalAsync(accrualId, token), memo, token);

        // loan instalments this run recovered go back on the loans
        foreach (var rec in await recoveries.Tracked(new Specification<PayrollLoanRecovery>(x => x.PayrollRunId == run.Id && x.Applied)).ToListAsync(token))
        {
            var loan = await loanRows.GetForUpdateAsync(rec.LoanId, token);
            loan.Remaining += rec.Amount;
            if (loan.Status == LoanStatus.Settled) loan.Status = LoanStatus.Active;
            rec.Applied = false;
        }
        run.Status = PayrollStatus.Voided;
        VoidMemo.Stamp(run, cmd.Reason, clock, user);
        VoidMemo.Audit(audit, user, clock, nameof(PayrollRun), run.Id, run.Number, cmd.Reason!);
        await runs.SaveChangesAsync(token);
        return PayrollHandlers.ToDto(run);
    }, ct);

    private async Task<JournalEntry> JournalAsync(Guid id, CancellationToken ct)
        => await journals.Tracked(new Specification<JournalEntry>(j => j.Id == id).Include(j => j.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    private async Task<PayrollRun> LoadAsync(Guid id, CancellationToken ct)
        => await runs.Tracked(new Specification<PayrollRun>(r => r.Id == id).Include(r => r.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
}
