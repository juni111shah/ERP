using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class CancelLoanHandler(IEmployeeLoanRepository loans, IEmployeeRepository employees) : ICommandHandler<CancelLoanCommand, LoanDto>
{
    public async Task<LoanDto> Handle(CancelLoanCommand command, CancellationToken ct)
    {
        var loan = await loans.GetForUpdateAsync(command.Id, ct);
        if (loan.Status != LoanStatus.Active) throw new ConflictException("Only an active loan can be cancelled.");
        loan.Status = LoanStatus.Cancelled;
        await loans.SaveChangesAsync(ct);
        return LoanDto.From(loan, await employees.GetNameAsync(loan.EmployeeId, ct));
    }
}
