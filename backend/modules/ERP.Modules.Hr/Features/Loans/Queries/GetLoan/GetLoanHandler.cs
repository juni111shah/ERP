using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetLoanHandler(IEmployeeLoanRepository loans, IEmployeeRepository employees) : IQueryHandler<GetLoanQuery, LoanDto>
{
    public async Task<LoanDto> Handle(GetLoanQuery query, CancellationToken ct)
    {
        var l = await loans.FindAsync(query.Id, ct) ?? throw new NotFoundException();
        return LoanDto.From(l, await employees.GetNameAsync(l.EmployeeId, ct));
    }
}
