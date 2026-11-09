using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateLoanHandler(IEmployeeLoanRepository loans, IReadRepository<Employee> employees, ILegalEntityGuard guard) : ICommandHandler<CreateLoanCommand, LoanDto>
{
    public async Task<LoanDto> Handle(CreateLoanCommand command, CancellationToken ct)
    {
        // 1. Only for someone still employed; the instalments must cover the amount.
        var i = command.Input;
        var emp = await employees.FindAsync(i.EmployeeId, ct) ?? throw new FieldValidationException("employeeId", "Choose an employee.");
        if (emp.Status == EmployeeStatus.Terminated || emp.TerminationDate is not null) throw new ConflictException("A loan cannot be given to an employee who has left.");
        var entity = await guard.RequireAsync(emp.LegalEntityId, ct);
        var instalment = i.InstalmentAmount ?? LoanMath.DefaultInstalment(i.Principal, i.Instalments);
        if (instalment * i.Instalments < i.Principal) throw new FieldValidationException("instalmentAmount", "The instalments do not add up to the amount borrowed.");

        // 2. Add and save.
        var loan = new EmployeeLoan
        {
            LegalEntityId = entity.Id, EmployeeId = emp.Id, Kind = i.Kind, Principal = i.Principal, InstalmentAmount = instalment, Instalments = i.Instalments,
            StartYear = i.StartYear, StartMonth = i.StartMonth, Remaining = i.Principal, Status = LoanStatus.Active, Reason = i.Reason?.Trim(),
        };
        loans.Add(loan);
        await loans.SaveChangesAsync(ct);
        return LoanDto.From(loan, emp.FullName);
    }
}
