using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class EmployeeLoanRepository(IRepository<EmployeeLoan> inner) : RepositoryBase<EmployeeLoan>(inner), IEmployeeLoanRepository
{
    public Task<List<OpenLoanBalance>> ListOpenBalancesAsync(Guid employeeId, CancellationToken ct)
        => Query(new Specification<EmployeeLoan>(l => l.EmployeeId == employeeId && l.Status == LoanStatus.Active && l.Remaining > 0)).Select(l => new OpenLoanBalance(l.Remaining, l.DisbursedOn)).ToListAsync(ct);

    public async Task<List<EmployeeLoan>> ListRecoverableOldestFirstAsync(Guid employeeId, CancellationToken ct)
        => (await Query(new Specification<EmployeeLoan>(l => l.EmployeeId == employeeId && l.Status == LoanStatus.Active && l.Remaining > 0 && l.DisbursedOn != null)).ToListAsync(ct))
            .OrderBy(l => l.StartYear).ThenBy(l => l.StartMonth).ToList();

    public Task<List<EmployeeLoan>> ListRecoverableOfEntityAsync(Guid legalEntityId, List<Guid> employeeIds, CancellationToken ct)
        => Query(new Specification<EmployeeLoan>(l => l.LegalEntityId == legalEntityId && l.Status == LoanStatus.Active && l.Remaining > 0 && l.DisbursedOn != null && employeeIds.Contains(l.EmployeeId))).ToListAsync(ct);
}
