using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class PayrollLoanRecoveryRepository(IRepository<PayrollLoanRecovery> inner) : RepositoryBase<PayrollLoanRecovery>(inner), IPayrollLoanRecoveryRepository
{
    public Task<List<PayrollLoanRecovery>> ListForRunForUpdateAsync(Guid runId, CancellationToken ct)
        => Tracked(new Specification<PayrollLoanRecovery>(x => x.PayrollRunId == runId)).ToListAsync(ct);

    public Task<List<PayrollLoanRecovery>> ListUnappliedForRunForUpdateAsync(Guid runId, CancellationToken ct)
        => Tracked(new Specification<PayrollLoanRecovery>(x => x.PayrollRunId == runId && !x.Applied)).ToListAsync(ct);

    public Task<List<PayrollLoanRecovery>> ListAppliedForRunForUpdateAsync(Guid runId, CancellationToken ct)
        => Tracked(new Specification<PayrollLoanRecovery>(x => x.PayrollRunId == runId && x.Applied)).ToListAsync(ct);
}
