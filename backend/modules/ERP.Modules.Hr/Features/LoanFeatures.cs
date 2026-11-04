using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

/// <summary>InstalmentAmount defaults to Principal / Instalments. StartYear/StartMonth is the first payroll month that recovers an instalment.</summary>
public sealed record LoanInput(Guid EmployeeId, LoanKind Kind, decimal Principal, int Instalments, int StartYear, int StartMonth, decimal? InstalmentAmount = null, string? Reason = null);
public sealed record LoanDto(Guid Id, Guid EmployeeId, string EmployeeName, LoanKind Kind, decimal Principal, decimal InstalmentAmount, int StartYear, int StartMonth, int Instalments,
    decimal Remaining, LoanStatus Status, string? Reason, DateOnly? DisbursedOn, Guid? DisbursementJournalId);
public sealed record LoanRow(Guid Id, Guid EmployeeId, LoanKind Kind, decimal Principal, decimal Remaining, int StartYear, int StartMonth, int Instalments, LoanStatus Status, DateOnly? DisbursedOn);

public sealed record GetLoan(Guid Id) : IQuery<LoanDto>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
public sealed record CreateLoan(LoanInput Input) : ICommand<LoanDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
/// <summary>Stops recovery. The unrecovered balance stays on the loan for the record; instalments already in a posted run are not undone.</summary>
public sealed record CancelLoan(Guid Id) : ICommand<LoanDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
/// <summary>Pays the loan out: Dr employee advances, Cr bank. Once per loan.</summary>
public sealed record DisburseLoan(Guid Id, Guid BankAccountId, DateOnly Date) : ICommand<LoanDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => P.PayrollPay; }

public sealed class LoanInputValidator : AbstractValidator<LoanInput>
{
    public LoanInputValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Principal).GreaterThan(0).MustBeMoney();
        RuleFor(x => x.Instalments).InclusiveBetween(1, 120);
        RuleFor(x => x.StartYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.StartMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.InstalmentAmount!.Value).GreaterThan(0).When(x => x.InstalmentAmount is not null).WithName("InstalmentAmount");
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}
public sealed class CreateLoanValidator : AbstractValidator<CreateLoan> { public CreateLoanValidator() => RuleFor(x => x.Input).SetValidator(new LoanInputValidator()); }
public sealed class DisburseLoanValidator : AbstractValidator<DisburseLoan>
{
    public DisburseLoanValidator() { RuleFor(x => x.BankAccountId).NotEmpty().WithMessage("Choose the bank account."); RuleFor(x => x.Date).MustBePlausibleDate(); }
}

/// <summary>The instalment schedule: the recovery of one run never exceeds the balance or the pay that is left.</summary>
public static class LoanMath
{
    public static decimal DefaultInstalment(decimal principal, int instalments) => PayrollMath.R(principal / Math.Max(1, instalments));

    /// <summary>This month's recovery: the instalment, but never more than the balance left, and never more than <paramref name="availablePay"/> (what the employee can still lose without going below zero).</summary>
    public static decimal Recovery(decimal instalment, decimal remaining, decimal availablePay)
        => Math.Max(0m, Math.Min(Math.Min(instalment, remaining), availablePay));

    public static bool Started(EmployeeLoan l, int year, int month) => l.StartYear * 12 + l.StartMonth <= year * 12 + month;
}

internal sealed class LoanHandlers(
    IRepository<EmployeeLoan> loans, IReadRepository<Employee> employees, IReadRepository<Account> accounts, ILegalEntityGuard guard, IPostingService posting, IUnitOfWork uow) :
    IRequestHandler<GetLoan, LoanDto>,
    IRequestHandler<CreateLoan, LoanDto>,
    IRequestHandler<CancelLoan, LoanDto>,
    IRequestHandler<DisburseLoan, LoanDto>
{
    public async Task<LoanDto> Handle(GetLoan q, CancellationToken ct)
    {
        var l = await loans.FindAsync(q.Id, ct) ?? throw new NotFoundException();
        return ToDto(l, await NameAsync(l.EmployeeId, ct));
    }

    public async Task<LoanDto> Handle(CreateLoan cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var emp = await employees.FindAsync(i.EmployeeId, ct) ?? throw new FieldValidationException("employeeId", "Choose an employee.");
        if (emp.Status == EmployeeStatus.Terminated || emp.TerminationDate is not null) throw new ConflictException("A loan cannot be given to an employee who has left.");
        var entity = await guard.RequireAsync(emp.LegalEntityId, ct);
        var instalment = i.InstalmentAmount ?? LoanMath.DefaultInstalment(i.Principal, i.Instalments);
        if (instalment * i.Instalments < i.Principal) throw new FieldValidationException("instalmentAmount", "The instalments do not add up to the amount borrowed.");
        var loan = new EmployeeLoan
        {
            LegalEntityId = entity.Id, EmployeeId = emp.Id, Kind = i.Kind, Principal = i.Principal, InstalmentAmount = instalment, Instalments = i.Instalments,
            StartYear = i.StartYear, StartMonth = i.StartMonth, Remaining = i.Principal, Status = LoanStatus.Active, Reason = i.Reason?.Trim(),
        };
        loans.Add(loan);
        await loans.SaveChangesAsync(ct);
        return ToDto(loan, emp.FullName);
    }

    public async Task<LoanDto> Handle(CancelLoan cmd, CancellationToken ct)
    {
        var loan = await loans.GetForUpdateAsync(cmd.Id, ct);
        if (loan.Status != LoanStatus.Active) throw new ConflictException("Only an active loan can be cancelled.");
        loan.Status = LoanStatus.Cancelled;
        await loans.SaveChangesAsync(ct);
        return ToDto(loan, await NameAsync(loan.EmployeeId, ct));
    }

    public Task<LoanDto> Handle(DisburseLoan cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var loan = await loans.GetForUpdateAsync(cmd.Id, token);
        if (loan.Status != LoanStatus.Active) throw new ConflictException("Only an active loan can be paid out.");
        if (loan.DisbursedOn is not null) throw new ConflictException("This loan was already paid out.");
        var bank = await accounts.FindAsync(cmd.BankAccountId, token);
        if (bank is null || !BankAccountRules.IsCashOrBank(bank)) throw new FieldValidationException("bankAccountId", "Choose an active bank account.");
        var advances = await posting.AccountForRoleAsync("payroll.advances", token);
        var name = await NameAsync(loan.EmployeeId, token);
        var memo = $"{(loan.Kind == LoanKind.Advance ? "Salary advance" : "Staff loan")} to {name}";
        var journal = await posting.PostAsync(new PostingRequest(loan.LegalEntityId, cmd.Date, "payroll-loan", loan.Id, memo, null,
            [new PostingLine(advances, loan.Principal, 0, memo), new PostingLine(bank.Id, 0, loan.Principal, memo)]), token);
        loan.DisbursedOn = cmd.Date; loan.DisbursementJournalId = journal.Id;
        await loans.SaveChangesAsync(token);
        return ToDto(loan, name);
    }, ct);

    private async Task<string> NameAsync(Guid employeeId, CancellationToken ct)
        => await employees.Query(new Specification<Employee>(e => e.Id == employeeId)).Select(e => e.FullName).FirstOrDefaultAsync(ct) ?? "";

    internal static LoanDto ToDto(EmployeeLoan l, string employeeName) => new(l.Id, l.EmployeeId, employeeName, l.Kind, l.Principal, l.InstalmentAmount, l.StartYear, l.StartMonth, l.Instalments,
        l.Remaining, l.Status, l.Reason, l.DisbursedOn, l.DisbursementJournalId);
}
