using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class DisburseLoanHandler(IEmployeeLoanRepository loans, IEmployeeRepository employees, IReadRepository<Account> accounts, IPostingService posting, IUnitOfWork uow)
    : ICommandHandler<DisburseLoanCommand, LoanDto>
{
    public Task<LoanDto> Handle(DisburseLoanCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Only an active loan not yet paid out, from an active bank account.
        var loan = await loans.GetForUpdateAsync(command.Id, token);
        if (loan.Status != LoanStatus.Active) throw new ConflictException("Only an active loan can be paid out.");
        if (loan.DisbursedOn is not null) throw new ConflictException("This loan was already paid out.");
        var bank = await accounts.FindAsync(command.BankAccountId, token);
        if (bank is null || !BankAccountRules.IsCashOrBank(bank)) throw new FieldValidationException("bankAccountId", "Choose an active bank account.");

        // 2. Post Dr advances, Cr bank, and save.
        var advances = await posting.AccountForRoleAsync("payroll.advances", token);
        var name = await employees.GetNameAsync(loan.EmployeeId, token);
        var memo = $"{(loan.Kind == LoanKind.Advance ? "Salary advance" : "Staff loan")} to {name}";
        var journal = await posting.PostAsync(new PostingRequest(loan.LegalEntityId, command.Date, "payroll-loan", loan.Id, memo, null,
            [new PostingLine(advances, loan.Principal, 0, memo), new PostingLine(bank.Id, 0, loan.Principal, memo)]), token);
        loan.DisbursedOn = command.Date; loan.DisbursementJournalId = journal.Id;
        await loans.SaveChangesAsync(token);
        return LoanDto.From(loan, name);
    }, ct);
}
