using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for the loan instalments recovered by payroll lines.</summary>
public interface IPayrollLoanRecoveryRepository : IRepository<PayrollLoanRecovery>
{
    /// <summary>Every loan recovery of a run, tracked for changes.</summary>
    Task<List<PayrollLoanRecovery>> ListForRunForUpdateAsync(Guid runId, CancellationToken ct);

    /// <summary>The recoveries of a run not yet applied to their loans, tracked for changes.</summary>
    Task<List<PayrollLoanRecovery>> ListUnappliedForRunForUpdateAsync(Guid runId, CancellationToken ct);

    /// <summary>The recoveries of a run already applied to their loans, tracked for changes.</summary>
    Task<List<PayrollLoanRecovery>> ListAppliedForRunForUpdateAsync(Guid runId, CancellationToken ct);
}
