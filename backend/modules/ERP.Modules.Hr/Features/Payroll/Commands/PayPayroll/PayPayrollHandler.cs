using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class PayPayrollHandler(IPayrollRunRepository runs, IReadRepository<Account> accounts, IPostingService posting, IUnitOfWork uow, PayrollSeparation separation)
    : ICommandHandler<PayPayrollCommand, PayrollDto>
{
    public Task<PayrollDto> Handle(PayPayrollCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Only a posted (or partly paid) run, into an active bank account, not before the payroll month.
        var cmd = command;
        var run = await runs.GetWithLinesForUpdateAsync(cmd.Id, token);
        if (run.Status is PayrollStatus.Paid) throw new ConflictException("This payroll is already paid.");
        if (run.Status == PayrollStatus.Voided) throw new ConflictException("This payroll was voided.");
        if (run.Status is not (PayrollStatus.Posted or PayrollStatus.PartiallyPaid)) throw new ConflictException("Post the payroll before paying it.");
        await separation.RequireCanPayAsync(run, token);
        var bank = await accounts.FindAsync(cmd.BankAccountId, token);
        if (bank is null || !BankAccountRules.IsCashOrBank(bank)) throw new FieldValidationException("bankAccountId", "Choose an active bank account.");
        if (cmd.Date < new DateOnly(run.Year, run.Month, 1)) throw new FieldValidationException("date", "The payment date cannot be before the payroll month.");

        // 2. The lines to pay: the chosen employees, or every unpaid line.
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

        // 3. Post the payment, mark the lines paid, and save.
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
        return PayrollDto.From(run);
    }, ct);
}
